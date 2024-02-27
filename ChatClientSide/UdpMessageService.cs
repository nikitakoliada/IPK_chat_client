using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
namespace ChatClientSide
{
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
                        Console.WriteLine(result.RemoteEndPoint.ToString());
                        byte[] serverResponse = result.Buffer;
                        MessageType msgType = (MessageType)serverResponse[0];
                        int receivedMessageId = BitConverter.ToUInt16(new byte[] { serverResponse[1], serverResponse[2] }, 0);
                        if (msgType == MessageType.ERR || msgType == MessageType.MSG)
                        {
                            string receivedDisplayName = ExtractString(serverResponse, startIndex: 3);
                            string messageContents = ExtractString(serverResponse, startIndex: 3 + receivedDisplayName.Length + 1);
                            if (msgType == MessageType.ERR)
                            {
                                Console.WriteLine("ERROR FROM " + receivedDisplayName + ": " + messageContents);
                            }
                            else if (msgType == MessageType.MSG)
                            {
                                Console.WriteLine(receivedDisplayName + ": " + messageContents);
                            }
                            HandleConfirm(receivedMessageId);
                        }
                        else if (msgType == MessageType.BYE)
                        {
                            Console.WriteLine("Server has closed the connection.");
                            HandleConfirm(receivedMessageId);
                            Environment.Exit(0);
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

        public bool WaitConfirm(int messageId)
        {
            // Wait for the server's response
            var serverEndpoint = new IPEndPoint(IPAddress.Any, 0);
            //new port idk why TODO
            try
            {
                byte[] serverResponse = client.Receive(ref serverEndpoint);
                Console.WriteLine(serverResponse);
                // Check if the response is a "CONFIRM" message
                if (serverResponse.Length >= 3 && serverResponse[0] == (byte)MessageType.CONFIRM)
                {
                    // Extract the message ID from the response
                    byte[] responseMessageIdBytes = new byte[] { serverResponse[1], serverResponse[2] };
                    if (BitConverter.ToInt16(responseMessageIdBytes, 0) != messageId)
                    {
                        return false;
                    }
                    else
                    {
                        int responseMessageId = BitConverter.ToInt16(responseMessageIdBytes, 0);
                        return true;
                    }
                }
                return false;
            }
            catch (SocketException ex)
            {
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

        private bool WaitOnReply(int messageId)
        {
            IPEndPoint remoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
            bool messageReceived = false;
            int receivedMessageId = 0;
            bool messageStatus = false;
            while (messageReceived != true)
            {
                try
                {
                    byte[] receiveBytes = client.Receive(ref remoteIpEndPoint);

                    // Parsing the message according to the given structure
                    byte replyByte = receiveBytes[0];
                    receivedMessageId = BitConverter.ToUInt16(new byte[] { receiveBytes[2], receiveBytes[1] }, 0);
                    byte result = receiveBytes[3];
                    ushort refMessageId = BitConverter.ToUInt16(new byte[] { receiveBytes[5], receiveBytes[4] }, 0);
                    if (refMessageId != messageId)
                    {
                        continue;
                    }
                    // Assuming the message content starts at index 6 and is followed by a zero byte
                    int messageContentLength = Array.IndexOf(receiveBytes, (byte)0, 6) - 6;
                    string messageContent = Encoding.ASCII.GetString(receiveBytes, 6, messageContentLength);
                    messageReceived = true;
                    if (result == 1)
                    {
                        Console.WriteLine("Success: " + messageContent);
                    }
                    else if (result == 0)
                    {
                        Console.WriteLine("Failure: " + messageContent);
                    }
                }
                catch (SocketException ex)
                {
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
            byte[] expectedMessageIdBytes = Encoding.UTF8.GetBytes(string.Format("{0}", messageId));
            Array.Copy(expectedMessageIdBytes, 0, message, 1, expectedMessageIdBytes.Length);
            client.Send(message, message.Length);
        }

        // /auth xkolia00 90ac98ef-7d30-429a-8536-784ef48b43c3 tester_from_ohio_udp
        public override bool HandleAuth(string username, string secret)
        {
            var messageBuilder = new UdpMessageBuilder();
            messageBuilder.AddMessageType((byte)MessageType.AUTH);
            int messageId = GetMessageId();
            messageBuilder.AddMessageId(messageId);
            messageBuilder.AddStringWithDelimiter(username);
            messageBuilder.AddStringWithDelimiter(displayName);
            messageBuilder.AddStringWithDelimiter(secret);

            byte[] message = messageBuilder.GetMessage();

            int attempts = 0;
            client.Send(message, message.Length);
            if (!WaitConfirm(messageId))
            {
                while (attempts < maxRetransmissions)
                {
                    messageId = GetMessageId();
                    UdpMessageBuilder.ReplaceMessageId(message, messageId);
                    client.Send(message, message.Length);
                    if (WaitConfirm(messageId))
                    {
                        break;
                    }
                    else
                    {
                        if (attempts == maxRetransmissions)
                        {
                            Console.WriteLine("Failure: Auth failed.");
                            //TODO cuz i cant test it rn wont receive any response lol
                            //Environment.Exit(0);
                            return true;
                            break;
                        }
                        attempts++;
                    }
                }
            }
            //wait on reply from server
            bool messageStatus = WaitOnReply(messageId);
            if (messageStatus == false)
            {
                Console.WriteLine("Failure: Auth failed.");
                return false;
            }
            else
            {
                Console.WriteLine("Success: Auth success.");
                return true;
            }
            //probably 
        }

        public override void HandleJoin(string channelId)
        {
            //format the data to be sent
            var messageBuilder = new UdpMessageBuilder();
            messageBuilder.AddMessageType((byte)MessageType.JOIN);
            int messageId = GetMessageId();
            messageBuilder.AddMessageId(messageId);
            messageBuilder.AddStringWithDelimiter(channelId);
            messageBuilder.AddStringWithDelimiter(displayName);

            byte[] message = messageBuilder.GetMessage();
            // Send the message to the server
            int attempts = 0;
            client.Send(message, message.Length);
            if (!WaitConfirm(messageId))
            {
                while (attempts < maxRetransmissions)
                {
                    messageId = GetMessageId();
                    UdpMessageBuilder.ReplaceMessageId(message, messageId);
                    client.Send(message, message.Length);
                    if (WaitConfirm(messageId))
                    {
                        break;
                    }
                    else
                    {
                        if (attempts == maxRetransmissions)
                        {
                            Console.WriteLine("Failure: Join failed.");
                            //TODO cuz i cant test it rn wont receive any response lol
                            //Environment.Exit(0);
                            break;
                        }
                        attempts++;
                    }
                }
            }
            //wait on reply from server
            bool messageStatus = WaitOnReply(messageId);
            if (messageStatus == false)
            {
                Console.WriteLine("Failure: Join failed.");
            }
            else
            {
                Console.WriteLine("Success: Join success.");
            }
        }

        public override void HandleMsg(string messageContents)
        {
            //format the data to be sent
            var messageBuilder = new UdpMessageBuilder();
            messageBuilder.AddMessageType((byte)MessageType.MSG);
            int messageId = GetMessageId();
            messageBuilder.AddMessageId(messageId);
            messageBuilder.AddStringWithDelimiter(displayName);
            messageBuilder.AddStringWithDelimiter(messageContents);

            byte[] message = messageBuilder.GetMessage();

            //send message to server until confrimation received
            int attempts = 0;
            client.Send(message, message.Length);
            if (!WaitConfirm(messageId))
            {
                while (attempts < maxRetransmissions)
                {
                    messageId = GetMessageId();
                    UdpMessageBuilder.ReplaceMessageId(message, messageId);
                    client.Send(message, message.Length);
                    if (WaitConfirm(messageId))
                    {
                        break;
                    }
                    else
                    {
                        if (attempts == maxRetransmissions)
                        {
                            Console.WriteLine("Failure: Message failed.");
                            break;
                        }
                        attempts++;
                    }
                }
            }
        }

        public override void HandleBye()
        {
            //format the data to be sent
            var messageBuilder = new UdpMessageBuilder();
            messageBuilder.AddMessageType((byte)MessageType.BYE);
            int messageId = GetMessageId();
            messageBuilder.AddMessageId(messageId);
            byte[] message = messageBuilder.GetMessage();

            int attempts = 0;
            client.Send(message, message.Length);

            if (!WaitConfirm(messageId))
            {
                while (attempts < maxRetransmissions)
                {
                    messageId = GetMessageId();
                    UdpMessageBuilder.ReplaceMessageId(message, messageId);
                    client.Send(message, message.Length);
                    if (WaitConfirm(messageId))
                    {
                        break;
                    }
                    else
                    {
                        if (attempts == maxRetransmissions)
                        {
                            Console.WriteLine("Failure: BYE failed.");
                            break;
                        }
                        attempts++;
                    }
                }
            }
        }

        public override void Close()
        {
            client.Close();
        }
    }
}