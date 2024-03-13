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
        private string server;
        private int port;

        public int currentMessageId = 0; // unique message ids 

        public UdpMessageService(UdpClient client, int maxRetransmissions, int confirmationTimeout, string server, int port) : base(maxRetransmissions, confirmationTimeout)
        {
            this.server = server;
            this.port = port;
            this.client = client;
            this.client.Client.ReceiveTimeout = confirmationTimeout;
            this.client.Client.SendTimeout = confirmationTimeout;
        }

        public int GetMessageId()
        {
            currentMessageId++;
            return currentMessageId;
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
                    using (var cts = new CancellationTokenSource(client.Client.ReceiveTimeout)) // Set up a new CTS with 100 ms timeout
                    {
                        UdpReceiveResult result = await client.ReceiveAsync(cts.Token).ConfigureAwait(false);
                        byte[] serverResponse = result.Buffer;
                        MessageType msgType = (MessageType)serverResponse[0];
                        int receivedMessageId = BitConverter.ToUInt16(new byte[] { serverResponse[1], serverResponse[2] }, 0);
                        // if (msgType == MessageType.CONFIRM)
                        // {
                        //     Console.WriteLine("Received confirmation where it doesnt need to be " + receivedMessageId);
                        // }
                        if (msgType == MessageType.ERR || msgType == MessageType.MSG)
                        {
                            string receivedDisplayName = ExtractString(serverResponse, startIndex: 3);
                            string messageContents = ExtractString(serverResponse, startIndex: 3 + receivedDisplayName.Length + 1);
                            if (msgType == MessageType.ERR)
                            {
                                Console.WriteLine("ERR FROM " + receivedDisplayName + ": " + messageContents);
                            }
                            else if (msgType == MessageType.MSG)
                            {
                                Console.WriteLine(receivedDisplayName + ": " + messageContents);
                            }
                            HandleConfirm(receivedMessageId);
                        }
                        else if (msgType == MessageType.BYE)
                        {
                            //Console.WriteLine("Server has closed the connection.");
                            HandleConfirm(receivedMessageId);
                            client.Close();
                            Environment.Exit(0);
                            break;
                        }

                    }
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
                catch (IOException)
                {
                    return;
                }
                catch (SocketException ex)
                {
                    Console.WriteLine("ERR: " + ex.Message);

                }
            }

        }

        public bool WaitConfirm(int messageId)
        {
            try
            {

                //UdpReceiveResult result = await client.ReceiveAsync(cts.Token).ConfigureAwait(false);
                //byte[] serverResponse = result.Buffer;
                //port = result.RemoteEndPoint.Port;
                IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] serverResponse = client.Receive(ref endpoint);
                port = endpoint.Port;
                // Check if the response is a "CONFIRM" message
                if (serverResponse[0] == (byte)MessageType.CONFIRM)
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
            catch (OperationCanceledException)
            {
                //Console.WriteLine("Confirmation timed out");
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    //Console.WriteLine("Confirmation timed out");

                }
                else
                {
                    Console.WriteLine("ERR: " + ex.Message);
                }
            }
            return false;
        }

        private bool WaitOnReply(int messageId)
        {
            bool messageReceived = false;
            int receivedMessageId = 0;
            bool messageStatus = false;
            int attempts = 0;
            while (attempts < maxRetransmissions && !messageReceived)
            {
                attempts++;
                try
                {
                    IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 0);
                    byte[] receiveBytes = client.Receive(ref endpoint);
                    // Parsing the message according to the given structure
                    byte replyByte = receiveBytes[0];
                    receivedMessageId = BitConverter.ToUInt16(new byte[] { receiveBytes[1], receiveBytes[2] }, 0);
                    HandleConfirm(receivedMessageId);
                    if (replyByte != (byte)MessageType.REPLY)
                    {
                        if (replyByte == (byte)MessageType.MSG)
                        {
                            string receivedDisplayName = ExtractString(receiveBytes, startIndex: 3);
                            string messageContents = ExtractString(receiveBytes, startIndex: 3 + receivedDisplayName.Length + 1);
                            Console.WriteLine(receivedDisplayName + ": " + messageContents);
                        }
                        continue;
                    }
                    byte result = receiveBytes[3];
                    int refMessageId = BitConverter.ToUInt16(new byte[] { receiveBytes[4], receiveBytes[5] }, 0);
                    if (refMessageId != messageId)
                    {
                        continue;
                    }
                    // Assuming the message content starts at index 6 and is followed by a zero byte
                    int messageContentLength = Array.IndexOf(receiveBytes, (byte)0, 6) - 6;
                    string messageContent = Encoding.ASCII.GetString(receiveBytes, 6, messageContentLength);
                    messageReceived = true;
                    if ((int)result == 1)
                    {
                        messageStatus = true;
                        Console.WriteLine("Success: " + messageContent);
                    }
                    else if ((int)result == 0)
                    {
                        Console.WriteLine("Failure: " + messageContent);
                    }

                }
                catch (OperationCanceledException)
                {
                    //Console.WriteLine("Reply timed out");
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        continue;
                    }
                    else
                    {
                        Console.WriteLine("ERR: " + ex.Message);
                    }
                }
            }
            return messageStatus;
        }

        public void HandleConfirm(int messageId)
        {
            byte[] message = new byte[1 + 2];
            message[0] = (byte)MessageType.CONFIRM; // CONFIRM message type
            byte[] expectedMessageIdBytes = BitConverter.GetBytes((ushort)messageId);
            Array.Copy(expectedMessageIdBytes, 0, message, 1, expectedMessageIdBytes.Length);
            client.Send(message, message.Length, server, port);
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
            client.Send(message, message.Length, server, port);
            if (!WaitConfirm(messageId))
            {
                while (attempts < maxRetransmissions)
                {
                    messageId = GetMessageId();
                    UdpMessageBuilder.ReplaceMessageId(message, messageId);
                    client.Send(message, message.Length, server, port);
                    //Console.WriteLine("Resending auth message");
                    if (WaitConfirm(messageId))
                    {
                        break;
                    }
                    else
                    {
                        if (attempts == maxRetransmissions - 1)
                        {
                            Console.WriteLine("Failure: Authentification failed, maximum amount of retransmissions were sent.");
                            return true;
                        }
                        attempts++;
                    }
                }
            }
            //wait on reply from server
            return WaitOnReply(messageId);
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
            client.Send(message, message.Length, server, port);
            if (!WaitConfirm(messageId))
            {
                while (attempts < maxRetransmissions)
                {
                    messageId = GetMessageId();
                    UdpMessageBuilder.ReplaceMessageId(message, messageId);
                    client.Send(message, message.Length, server, port);
                    if (WaitConfirm(messageId))
                    {
                        break;
                    }
                    else
                    {
                        if (attempts == maxRetransmissions - 1)
                        {
                            Console.WriteLine("Failure: Joining failed, maximum amount of retransmissions were sent.");
                            break;
                        }
                        attempts++;
                    }
                }
            }
            //wait on reply from server
            bool messageStatus = WaitOnReply(messageId);
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
            client.Send(message, message.Length, server, port);
            if (!WaitConfirm(messageId))
            {
                while (attempts < maxRetransmissions)
                {
                    messageId = GetMessageId();
                    UdpMessageBuilder.ReplaceMessageId(message, messageId);
                    client.Send(message, message.Length, server, port);
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
            client.Send(message, message.Length, server, port);

            if (!WaitConfirm(messageId))
            {
                while (attempts < maxRetransmissions)
                {
                    messageId = GetMessageId();
                    UdpMessageBuilder.ReplaceMessageId(message, messageId);
                    client.Send(message, message.Length, server, port);
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