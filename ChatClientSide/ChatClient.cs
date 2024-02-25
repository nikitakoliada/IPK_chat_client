using System.Net.Sockets;

namespace ChatClientSide
{
    
    class ChatClient
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
                Console.WriteLine("Error: Missing mandatory arguments. -t and -s are required.");
                Environment.Exit(1); // Exit with an error code
            }

            // Additional validation for transport protocol
            if (transportProtocol != "tcp" && transportProtocol != "udp")
            {
                Console.WriteLine("Error: wrong transport protocol");
                Environment.Exit(1); // Exit with an error code
                return;
            }
            MessageService messageService = new MessageService(maxRetransmissions, confirmationTimeout);

            if (transportProtocol == "udp")
            {
                //  UDP client code
                try{
                    UdpClient client = new UdpClient();
                    client.Connect(server, port);
                    messageService = new UdpMessageService(client, maxRetransmissions, confirmationTimeout);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error: " + e.Message);
                    Environment.Exit(1);
                }
            }
            else{
                //  TCP client code
                try{
                    TcpClient client = new TcpClient();
                    client.Connect(server, port);
                    NetworkStream stream = client.GetStream();
                    messageService = new TcpMessageService(client, stream, maxRetransmissions, confirmationTimeout);
                    
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error: " + e.Message);
                    Environment.Exit(1);
                }
            }
            bool running = true;

            while (running)
                {
                    try{
                        
                        CancellationTokenSource cts = new CancellationTokenSource();
                        var listeningTask = messageService.StartListening(cts.Token); // Start listening without awaiting
                        string input = Console.ReadLine();
                        string[] inputs = input.Split(' ', 2);
                        string command = inputs[0].Trim();
                        switch (command)
                        {
                            case "/auth":
                                string username = inputs[1].Split(' ', 2)[0].Trim();
                                if(username.Length > 20){
                                    Console.WriteLine("Username is longer than 20");
                                    break;
                                }
                                string secret = inputs[1].Split(' ', 3)[1].Trim();
                                if(secret.Length > 128){
                                    Console.WriteLine("Password is longer than 128");
                                    break;
                                }
                                string tryDisplayName = inputs[1].Split(' ', 3)[2].Trim();
                                if(tryDisplayName.Length > 128){
                                    Console.WriteLine("Display name is longer than 20");
                                    break;
                                }
                                messageService.displayName = tryDisplayName;
                                cts.Cancel();
                                if(messageService.HandleAuth(username, secret) == true)
                                    authorised = true;
                                break;
                            case "/join":
                                if(authorised == false){
                                    Console.WriteLine("Error: You are not authorised to join a channel");
                                    break;
                                }
                                string channelId = inputs[1].Split(' ', 2)[0].Trim();
                                if(channelId.Length > 128){
                                    Console.WriteLine("ChannelId name is longer than 20");
                                    break;
                                }
                                if(string.IsNullOrWhiteSpace(inputs[1].Split(' ', 2)[1].Trim())){
                                    MessageService.PrintHelp();
                                }
                                cts.Cancel();
                                messageService.HandleJoin(channelId);                                       
                                break;
                            case "/rename":
                                if(authorised == false){
                                    Console.WriteLine("Error: You are not authorised to rename");
                                    break;
                                }
                                string renameDisplayName = inputs[1];
                                if(renameDisplayName.Length > 128){
                                    Console.WriteLine("Display name is longer than 20");
                                    break;
                                }
                                
                                messageService.displayName = renameDisplayName;
                            
                                break;
                            case "/bye":
                                cts.Cancel();
                                messageService.HandleBye();
                                running = false;
                                break;
                            default:
                                if(authorised == false){
                                    Console.WriteLine("Error: You are not authorised to send messages");
                                    break;
                                }
                                cts.Cancel();
                                messageService.HandleMsg(input);
                                break;
                        }
                        if(cts.IsCancellationRequested){
                            try{
                                cts.Cancel();
                            }
                            catch{
                                //do nothing
                            }
                        }

                    }
                    catch(Exception e){
                        Console.WriteLine("Error: " + e.Message);
                        break;
                    }
                }
            messageService.Close();
        }
        
        
    }
}