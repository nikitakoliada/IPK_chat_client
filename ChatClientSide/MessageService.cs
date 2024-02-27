public class MessageService
{
    public int maxRetransmissions = 3;
    public string displayName = "";
    public int confirmationTimeout = 250;

    public MessageService(int maxRetransmissions, int confirmationTimeout)
    {
        this.maxRetransmissions = maxRetransmissions;
        this.confirmationTimeout = confirmationTimeout;
    }

    public virtual async Task StartListening(CancellationToken cancellationToken)
    {
        // Start listening for messages from the server
    }
    public virtual void HandleMsg(string msg)
    {
        // Handle a message from the server
    }
    public virtual void HandleJoin(string msg)
    {
        // Handle an error message from the server
    }
    public virtual void HandleBye()
    {
        // Handle a bye message from the server
    }
    public virtual bool HandleAuth(string username, string secret)
    {
        // Handle an authentication message from the server
        return false;
    }
    public static void PrintHelp()
    {
        // Print out help information for all supported commands
        Console.WriteLine("/auth {Username} {Secret} {DisplayName} - Authenticate with the server.");
        Console.WriteLine("/join {ChannelID} - Join a chat channel.");
        Console.WriteLine("/rename {DisplayName} - Change your display name.");
        Console.WriteLine("/help - Show this help message.");
        Console.WriteLine("Any other text will be sent as messages");
    }

    public virtual void Close()
    {
        // Close the connection to the server
    }
}