using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class UdpMessageService : MessageService
    {
        enum MessageType
        {
            CONFIRM = 0x00,
            REPLY = 0x01,
            AUTH = 0x02,
            JOIN = 0x03,
            MSG = 0x04,
            ERR = 0xFE,
            BYE = 0xFF
        }
        private UdpClient client;
        public int messageId = 0; // unique message ids 

        public UdpMessageService(UdpClient client, int maxRetransmissions, int confirmationTimeout) : base(maxRetransmissions, confirmationTimeout)
        {
            this.client = client;
            this.client.Client.ReceiveTimeout = confirmationTimeout;
        }
    
        public int GetMessageId()
        {
            return Interlocked.Increment(ref messageId);
        }

        private static string ExtractString(byte[] bytes, int startIndex)
        {
            // Find the null terminator
            int nullIndex = Array.IndexOf(bytes, (byte)0, startIndex);
            if (nullIndex == -1)
            {
                // Handle the case where there's no null terminator
                throw new Exception("Null terminator not found");
            }

            // Extract the string
            int stringLength = nullIndex - startIndex;
            return System.Text.Encoding.UTF8.GetString(bytes, startIndex, stringLength);
        }
        public override async Task StartListening(CancellationToken cancellationToken)
        {
            
            while (!cancellationToken.IsCancellationRequested)
            {
                // Wait for a message from the server
                try
                {
                    using (var cts = new CancellationTokenSource(250)) // Set up a new CTS with 250 ms timeout
                    {           
                        UdpReceiveResult result = await client.ReceiveAsync(cts.Token).ConfigureAwait(false);
                        byte[] serverResponse = result.Buffer;
                        MessageType msgType = (MessageType)serverResponse[0];
                        switch (msgType)
                        {
                            // call a function that will handle the messages TODO
                            case MessageType.ERR:
                                // Convert the next 2 bytes to MessageID
                                int eceivedMessageId = BitConverter.ToUInt16(new byte[] { serverResponse[1], serverResponse[2] }, 0);

                                // The rest of the method would depend on how DisplayName and MessageContents are encoded
                                // For example, if they are null-terminated strings in the byte array, you would
                                // find the index of the null terminator and convert the bytes to strings

                                // This is just a placeholder for the actual extraction logic
                                string receivedDisplayName = ExtractString(serverResponse, startIndex: 3);
                                string messageContents = ExtractString(serverResponse, startIndex: 3 + receivedDisplayName.Length + 1);

                                break;
                            case MessageType.MSG:
                                Console.WriteLine("Received message with unknown type " + serverResponse[0]);
                                break;
                        }
                    }
                }
                catch (OperationCanceledException ex)
                {
                    continue;
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        Console.WriteLine("Error: " + ex.Message);

                    }
                    else
                    {
                        Console.WriteLine("Error: " + ex.Message);
                    }
                }
            }
            
        }

        public bool SendAndWaitConfirm(byte[] message, int messageId){
            // Send the message to the server
            client.Send(message, message.Length);
            // Wait for the server's response
            var serverEndpoint = new IPEndPoint(IPAddress.Any, 0);
            //new port idk why TODO
            try{
                byte[] serverResponse = client.Receive(ref serverEndpoint);
                // Check if the response is a "CONFIRM" message
                if (serverResponse.Length >= 3 && serverResponse[0] == (byte)MessageType.CONFIRM)
                {
                    // Extract the message ID from the response
                    byte[] responseMessageIdBytes = new byte[] { serverResponse[1], serverResponse[2] };
                    if(BitConverter.ToInt16(responseMessageIdBytes, 0) != messageId){
                        return false;
                    }
                    else{
                        int responseMessageId = BitConverter.ToInt16(responseMessageIdBytes, 0);
                        return true;
                    }
                }
            return false;
            }
            catch (SocketException ex){
                if (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    return false;
                }
                else
                {
                    Console.WriteLine("Error: " + ex.Message);
                }
            }
            return false;
        }

        private bool WaitOnReply( int messageId)
        {
            IPEndPoint remoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
            bool messageReceived = false;
            int receivedMessageId = 0;
            bool messageStatus = false;
            while(messageReceived != true){
                try
                {
                    byte[] receiveBytes = client.Receive(ref remoteIpEndPoint);

                    // Parsing the message according to the given structure
                    byte replyByte = receiveBytes[0];
                    receivedMessageId = BitConverter.ToUInt16(new byte[] { receiveBytes[2], receiveBytes[1] }, 0);
                    byte result = receiveBytes[3];
                    ushort refMessageId = BitConverter.ToUInt16(new byte[] { receiveBytes[5], receiveBytes[4] }, 0);
                    if(refMessageId != messageId){
                        continue;
                    }
                    // Assuming the message content starts at index 6 and is followed by a zero byte
                    int messageContentLength = Array.IndexOf(receiveBytes, (byte)0, 6) - 6;
                    string messageContent = Encoding.ASCII.GetString(receiveBytes, 6, messageContentLength);
                    messageReceived = true;
                    if(result == 1){
                        Console.WriteLine("Success: " + messageContent);
                    }
                    else if(result == 0){
                        Console.WriteLine("Failure: " + messageContent);
                    }
                }
                catch (SocketException ex){
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                        {
                            continue;
                        }
                        else
                        {
                            Console.WriteLine("Error: " + ex.Message);
                        }
                }
            }
            HandleConfirm(receivedMessageId);

            return messageStatus;
        }

        public void HandleConfirm(int messageId)
        {
            byte[] message = new byte[1 + 2];
            message[0] = (byte)MessageType.CONFIRM; // CONFIRM message type
            byte[] expectedMessageIdBytes = Encoding.UTF8.GetBytes(string.Format("{0}",messageId));
            Array.Copy(expectedMessageIdBytes, 0, message, 1, expectedMessageIdBytes.Length);
            client.Send(message, message.Length);
        }

        public override bool HandleAuth(string username, string secret)
        {    
            //format the data to be sent
            byte[] usernameBytes = Encoding.UTF8.GetBytes(username);
            byte[] displayNameBytes = Encoding.UTF8.GetBytes(displayName);
            byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
            byte[] message = new byte[1 + 2 + usernameBytes.Length + 1 + displayNameBytes.Length + 1 + secretBytes.Length + 1];
            message[0] = (byte)MessageType.AUTH; // AUTH message type
            int messageId = GetMessageId(); 
            byte[] expectedMessageIdBytes = Encoding.UTF8.GetBytes(string.Format("{0}",messageId));
            Array.Copy(expectedMessageIdBytes, 0, message, 1, expectedMessageIdBytes.Length);
            Array.Copy(usernameBytes, 0, message, 3, usernameBytes.Length);
            message[3 + usernameBytes.Length] = 0; 
            Array.Copy(displayNameBytes, 0, message, 4 + usernameBytes.Length, displayNameBytes.Length);
            message[4 + usernameBytes.Length + displayNameBytes.Length] = 0; 
            Array.Copy(secretBytes, 0, message, 5 + usernameBytes.Length + displayNameBytes.Length, secretBytes.Length);
            message[5 + usernameBytes.Length + displayNameBytes.Length + secretBytes.Length] = 0; 
            //send message to server until confrimation received
            int attempts = 0;
            while(attempts <= maxRetransmissions){
                messageId = GetMessageId(); 
                expectedMessageIdBytes = Encoding.UTF8.GetBytes(string.Format("{0}",messageId));
                Array.Copy(expectedMessageIdBytes, 0, message, 1, expectedMessageIdBytes.Length);
                if(SendAndWaitConfirm(message, messageId)){
                    break;
                }
                else{
                    if(attempts == maxRetransmissions){
                        Console.WriteLine("Failure: Auth failed.");
                        //TODO cuz i cant test it rn wont receive any response lol
                        //Environment.Exit(0);
                        return true;
                        break;
                    }
                    attempts++;
                }
            }
            //wait on reply from server
            bool messageStatus = WaitOnReply(messageId);
            if(messageStatus == false){
                Console.WriteLine("Failure: Auth failed.");
                return false;
            }
            else{
                Console.WriteLine("Success: Auth success.");
                return true;
            }
            //probably 
        }

        public override void HandleJoin(string channelId)
        {
            //format the data to be sent
            byte[] channelIdBytes = Encoding.UTF8.GetBytes(channelId);
            byte[] displayNameBytes = Encoding.UTF8.GetBytes(displayName);

            byte[] message = new byte[1 + 2 + channelIdBytes.Length + 1 + displayNameBytes.Length + 1];
            message[0] = (byte)MessageType.JOIN; // JOIN message type
            int messageId = GetMessageId(); 
            byte[] expectedMessageIdBytes = Encoding.UTF8.GetBytes(string.Format("{0}",messageId));
            Array.Copy(expectedMessageIdBytes, 0, message, 1, expectedMessageIdBytes.Length);
            Array.Copy(channelIdBytes, 0, message, 3, channelIdBytes.Length);
            message[3 + channelIdBytes.Length] = 0; // Zero byte delimiter
            Array.Copy(displayNameBytes, 0, message, 4 + channelIdBytes.Length, displayNameBytes.Length);
            message[4 + channelIdBytes.Length + displayNameBytes.Length] = 0; // Zero byte delimiter

            // Send the message to the server
            int attempts = 0;
            while(attempts <= maxRetransmissions){
                messageId = GetMessageId(); 
                expectedMessageIdBytes = Encoding.UTF8.GetBytes(string.Format("{0}",messageId));
                Array.Copy(expectedMessageIdBytes, 0, message, 1, expectedMessageIdBytes.Length);
                if(SendAndWaitConfirm(message, messageId)){
                    break;
                }
                else{
                    if(attempts == maxRetransmissions){
                        Console.WriteLine("Failure: Join failed.");
                    }
                    attempts++;
                }
            }
            //wait on reply from server
            bool messageStatus = WaitOnReply(messageId);
            if(messageStatus == false){
                Console.WriteLine("Failure: Join failed.");
            }
            else{
                Console.WriteLine("Success: Join success.");
            }
        }

        public override void HandleMsg(string messageContents )
        {
            //format the data to be sent
            byte[] displayNameBytes = Encoding.UTF8.GetBytes(displayName);
            byte[] messageContentsBytes = Encoding.UTF8.GetBytes(messageContents);

            byte[] message = new byte[1 + 2 + displayNameBytes.Length + 1 + messageContentsBytes.Length + 1];
            message[0] = (byte)MessageType.MSG; // MSG message type
            int messageId = GetMessageId(); 
            byte[] expectedMessageIdBytes = Encoding.UTF8.GetBytes(string.Format("{0}",messageId));
            Array.Copy(expectedMessageIdBytes, 0, message, 1, expectedMessageIdBytes.Length);
            Array.Copy(displayNameBytes, 0, message, 3, displayNameBytes.Length);
            message[3 + displayNameBytes.Length] = 0; // Zero byte delimiter
            Array.Copy(messageContentsBytes, 0, message, 4 + displayNameBytes.Length, messageContentsBytes.Length);
            message[4 + displayNameBytes.Length + messageContentsBytes.Length] = 0; // Zero byte delimiter

            //send message to server until confrimation received
            int attempts = 0;
            while(attempts <= maxRetransmissions){
                messageId = GetMessageId(); 
                expectedMessageIdBytes = Encoding.UTF8.GetBytes(string.Format("{0}",messageId));
                Array.Copy(expectedMessageIdBytes, 0, message, 1, expectedMessageIdBytes.Length);
                if(SendAndWaitConfirm(message, messageId)){
                    break;
                }
                else{
                    if(attempts == maxRetransmissions){
                        Console.WriteLine("Failure: Msg failed.");
                    }
                    attempts++;
                }
            }
        }

        public override  void HandleBye()
        {
            //format the data to be sent
            byte[] message = new byte[1 + 2];
            message[0] = (byte)MessageType.BYE; // Bye message type
            int messageId = GetMessageId(); 
            byte[] expectedMessageIdBytes = Encoding.UTF8.GetBytes(string.Format("{0}",messageId));
            Array.Copy(expectedMessageIdBytes, 0, message, 1, expectedMessageIdBytes.Length);
            //send message to server until confrimation received
            int attempts = 0;
            while(attempts <= maxRetransmissions){
                messageId = GetMessageId(); 
                expectedMessageIdBytes = Encoding.UTF8.GetBytes(string.Format("{0}",messageId));
                Array.Copy(expectedMessageIdBytes, 0, message, 1, expectedMessageIdBytes.Length);
                if(SendAndWaitConfirm(message, messageId)){
                    break;
                }
                else{
                    if(attempts == maxRetransmissions){
                        Console.WriteLine("Failure: Auth failed.");
                        Environment.Exit(0);
                    }
                    attempts++;
                }
            }
            //wait on reply from server
        }
        
        public override void Close()
        {
            client.Close();
        }
    }
