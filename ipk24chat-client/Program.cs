using System.Buffers;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;

namespace ChatClientSide
{

    class Program
    {
        public static void PrintHelpForArg()
        {
            Console.WriteLine("Usage: ChatClient -t [tcp/udp] -s [server] -p [port] -d [udpConfirmationTimeout] -r [maxRetransmissions]");
            Console.WriteLine("  -t: Transport protocol used for connection");
            Console.WriteLine("  -s: Server IP or hostname");
            Console.WriteLine("  -p: Server port");
            Console.WriteLine("  -d: UDP confirmation timeout");
            Console.WriteLine("  -r: Maximum number of UDP retransmissions");
            Console.WriteLine("  -h: Prints program help output and exits");
        }
        static void Main(string[] args)
        {
            string transportProtocol = "udp";
            int port = 4567;
            string server = "localhost";
            bool authorised = false;
            int confirmationTimeout = 250;
            int maxRetransmissions = 3;
            bool tFlag = false, sFlag = false; // Flags to indicate if the mandatory args are set

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-t":
                        transportProtocol = args[++i];
                        tFlag = true; // Set the flag to true since -t is provided
                        break;
                    case "-s":
                        server = args[++i];
                        sFlag = true; // Set the flag to true since -s is provided
                        break;
                    case "-p":
                        port = int.Parse(args[++i]);
                        break;
                    case "-d":
                        confirmationTimeout = int.Parse(args[++i]);
                        break;
                    case "-r":
                        maxRetransmissions = int.Parse(args[++i]);
                        break;
                    case "-h":
                        PrintHelpForArg();
                        Environment.Exit(0);
                        return;
                }
            }
            // Check if the mandatory arguments are set
            if (!tFlag || !sFlag)
            {
                Console.WriteLine("ERR: Missing mandatory arguments. -t and -s are required.");
                Environment.Exit(1); // Exit with an error code
            }

            // Additional validation for transport protocol
            if (transportProtocol != "tcp" && transportProtocol != "udp")
            {
                Console.WriteLine("ERR: wrong transport protocol");
                Environment.Exit(1); // Exit with an error code
                return;
            }
            MessageService messageService = new MessageService(maxRetransmissions, confirmationTimeout);

            if (transportProtocol == "udp")
            {
                //  UDP client code
                try
                {
                    UdpClient client = new UdpClient();
                    messageService = new UdpMessageService(client, maxRetransmissions, confirmationTimeout, server, port);
                }
                catch (Exception e)
                {
                    Console.WriteLine("ERR: " + e.Message);
                    Environment.Exit(1);
                }
            }
            else
            {
                //  TCP client code
                try
                {
                    TcpClient client = new TcpClient();
                    client.Connect(server, port);
                    NetworkStream stream = client.GetStream();
                    messageService = new TcpMessageService(client, stream, maxRetransmissions, confirmationTimeout);

                }
                catch (Exception e)
                {
                    Console.WriteLine("ERR: " + e.Message);
                    Environment.Exit(1);
                }
            }
            bool running = true;
            Console.CancelKeyPress += new ConsoleCancelEventHandler((sender, e) => CancellationHandler(sender, e, authorised, messageService));
            while (running)
            {
                try
                {
                    CancellationTokenSource cts = new CancellationTokenSource();
                    var listeningTask = messageService.StartListening(cts.Token); // Start listening without awaiting
                    // if the input is from file we need to wait for the server to reply
                    Thread.Sleep(250);
                    string? input = Console.ReadLine();
                    Console.WriteLine(input);
                    if (input == null)
                    {
                        continue;
                    }
                    string[] inputs = input.Split(' ', 2);
                    string command = inputs[0].Trim();
                    if (inputs.Length > 1)
                        inputs[1] = inputs[1].Trim();
                    switch (command)
                    {
                        case "/auth":
                            try
                            {
                                if(authorised == true)
                                {
                                    Console.WriteLine("ERR: You are already authorised");
                                    break;
                                }
                                var elements = inputs[1].Split(' ');
                                if (elements.Length != 3)
                                {
                                    Console.WriteLine("ERR: Wrong amount of elements for the commmand");
                                    break;
                                }
                                string username = elements[0].Trim();
                                if (username.Length > 20)
                                {
                                    Console.WriteLine("ERR: Username is longer than 20");
                                    break;
                                }
                                string secret = elements[1].Trim();
                                if (secret.Length > 128)
                                {
                                    Console.WriteLine("ERR: Password is longer than 128");
                                    break;
                                }
                                string tryDisplayName = elements[2].Trim();
                                if (tryDisplayName.Length > 128)
                                {
                                    Console.WriteLine("ERR: Display name is longer than 20");
                                    break;
                                }
                                messageService.displayName = tryDisplayName;
                                cts.Cancel();
                                if (messageService.HandleAuth(username, secret) == true)
                                    authorised = true;
                                break;
                            }
                            catch (IndexOutOfRangeException)
                            {
                                Console.WriteLine("ERR: Wrong amount of elements for the commmand");
                                continue;
                            }

                        case "/join":
                            try
                            {
                                var elements = inputs[1].Split(' ');
                                if (elements.Length != 1)
                                {
                                    Console.WriteLine("ERR: Wrong amount of elements for the commmand");
                                    break;
                                }
                                if (authorised == false)
                                {
                                    Console.WriteLine("ERR: You are not authorised to join a channel");
                                    break;
                                }
                                string channelId = elements[0].Trim();
                                if (channelId.Length > 128)
                                {
                                    Console.WriteLine("ERR: ChannelId name is longer than 20");
                                    break;
                                }
                                cts.Cancel();
                                listeningTask.Wait();
                                messageService.HandleJoin(channelId);
                                break;
                            }
                            catch (IndexOutOfRangeException)
                            {
                                Console.WriteLine("ERR: Wrong amount of elements for the commmand");
                                continue;
                            }
                        case "/rename":
                            try
                            {
                                var elements = inputs[1].Split(' ');
                                if (elements.Length != 1)
                                {
                                    Console.WriteLine("ERR: Wrong amount of elements for the commmand");
                                    break;
                                }
                                cts.Cancel();
                                if (authorised == false)
                                {
                                    Console.WriteLine("ERR: You are not authorised to rename");
                                    break;
                                }
                                string renameDisplayName = inputs[1];
                                if (renameDisplayName.Length > 128)
                                {
                                    Console.WriteLine("ERR: Display name is longer than 20");
                                    break;
                                }
                                listeningTask.Wait();
                                messageService.displayName = renameDisplayName;

                                break;
                            }
                            catch (IndexOutOfRangeException)
                            {
                                Console.WriteLine("ERR: Wrong amount of elements for the commmand");
                                continue;
                            }
                        case "/bye":
                            if (inputs.Length != 1)
                            {
                                Console.WriteLine("ERR: Wrong amount of elements for the commmand");
                                break;
                            }
                            cts.Cancel();
                            listeningTask.Wait();
                            messageService.HandleBye();
                            running = false;
                            break;
                        case "/help":
                            if (inputs.Length != 1)
                            {
                                Console.WriteLine("ERR: Wrong amount of elements for the commmand");
                                break;
                            }
                            MessageService.PrintHelp();
                            break;
                        default:
                            if (authorised == false)
                            {
                                Console.WriteLine("ERR: You are not authorised to send messages");
                                break;
                            }
                            cts.Cancel();
                            listeningTask.Wait();
                            messageService.HandleMsg(input);
                            break;
                    }
                    if (cts.IsCancellationRequested)
                    {
                        try
                        {
                            cts.Cancel();
                        }
                        catch
                        {
                            //do nothing
                        }
                    }

                }
                catch (OperationCanceledException)
                {
                    continue;
                }
                catch (Exception e)
                {
                    Console.WriteLine("ERR: " + e.Message);
                    break;
                }
            }
            messageService.Close();
        }

        private static void CancellationHandler(object? sender, ConsoleCancelEventArgs e, bool authorised, MessageService messageService)
        {
            try
            {
                if (authorised == true)
                {

                    messageService.HandleBye();
                    messageService.Close();
                    return;
                }
                else
                {
                    messageService.Close();
                    return;
                }
            }
            catch (Exception)
            {
                return;
            }
        }
    }
}