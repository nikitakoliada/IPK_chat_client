using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

public class TcpMessageService : MessageService
{
    public TcpClient client;
    public NetworkStream stream;

    public TcpMessageService(TcpClient client, NetworkStream stream, int maxRetransmissions, int confirmationTimeout) : base(maxRetransmissions, confirmationTimeout)
    {
        this.client = client;
        this.stream = stream;
    }

    public override async Task StartListening(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[1024];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    Console.WriteLine("Server has closed the connection." + "\r\n");
                    break;
                }
                string response = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                HandleResponse(response);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERR " + ex.Message);
        }
    }
    public void HandleResponse(string responseData)
    {
        var parts = responseData.Split('\n');
        foreach (var response in parts)
        {

            if (response.Contains("MSG"))
            {
                string pattern = @"MSG FROM (\S+) IS (.+)";
                Regex regex = new Regex(pattern);

                // Match the regular expression pattern against a text string
                Match match = regex.Match(response);

                if (match.Success)
                {
                    string recDisplayName = match.Groups[1].Value.Trim();
                    string messageCnt = match.Groups[2].Value.Trim();
                    Console.WriteLine(recDisplayName + ": " + messageCnt);
                }
            }
            else if (response.Contains("REPLY OK IS"))
            {
                string pattern = @"REPLY OK IS (.+)";
                Regex regex = new Regex(pattern);

                // Match the regular expression pattern against a text string
                Match match = regex.Match(response);

                if (match.Success)
                {
                    string messageCnt = match.Groups[1].Value.Trim();
                    Console.WriteLine("Success: " + messageCnt);
                }
            }
            else if (response.Contains("REPLY NOK IS"))
            {
                string pattern = @"REPLY NOK IS (.+)";
                Regex regex = new Regex(pattern);

                // Match the regular expression pattern against a text string
                Match match = regex.Match(response);

                if (match.Success)
                {
                    string messageCnt = match.Groups[1].Value.Trim();
                    Console.WriteLine("Failure: " + messageCnt);
                }
            }
            else if (response.Contains("ERR FROM"))
            {
                string pattern = @"ERR FROM (\S+) IS (.+)";
                Regex regex = new Regex(pattern);

                // Match the regular expression pattern against a text string
                Match match = regex.Match(response);

                if (match.Success)
                {
                    string recDisplayName = match.Groups[1].Value.Trim();
                    string messageCnt = match.Groups[2].Value.Trim();
                    Console.WriteLine("ERR FROM " + recDisplayName + ": " + messageCnt);
                }
            }
            else if (response.Contains("BYE"))
            {
                stream.Close();
                client.Close();
                Environment.Exit(0);
            }
        }

    }

    public override bool HandleAuth(string username, string secret)
    {
        string message = string.Format("AUTH {0} AS {1} USING {2}\r\n", username.Trim(), displayName.Trim(), secret.Trim());
        byte[] data = Encoding.ASCII.GetBytes(message);
        stream.Write(data, 0, data.Length);
        //wait on reply from server

        byte[] responseBytes = new byte[1000];
        // if the input is from file we need to wait for the server to reply
        Thread.Sleep(250);
        int bytes = stream.Read(responseBytes, 0, responseBytes.Length);
        string responseData = Encoding.ASCII.GetString(responseBytes, 0, bytes);

        if (responseData.Contains("REPLY OK IS"))
        {
            HandleResponse(responseData);
            return true;
        }
        else
        {
            HandleResponse(responseData);
            return false;
        }
    }

    public override void HandleJoin(string channelId)
    {
        string message = string.Format("JOIN {0} AS {1}\r\n", channelId.Trim(), displayName.Trim());
        byte[] data = Encoding.ASCII.GetBytes(message);
        stream.Write(data, 0, data.Length);
        //wait on reply from server

        byte[] responseBytes = new byte[1000];
        // if the input is from file we need to wait for the server to reply
        Thread.Sleep(250);
        int bytes = stream.Read(responseBytes, 0, responseBytes.Length);
        string responseData = Encoding.ASCII.GetString(responseBytes, 0, bytes);
        HandleResponse(responseData);
        // bool gotReply = false;
        // // wait for a reply from the server ( doesnt send somhoew)
        // //while (!gotReply)
        // //{
        //     byte[] responseBytes = new byte[1000];
        //     int bytes = stream.Read(responseBytes, 0, responseBytes.Length);
        //     string responseData = Encoding.ASCII.GetString(responseBytes, 0, bytes);
        //     if (responseData.Contains("REPLY OK IS"))
        //     {
        //         responseData = responseData.Replace("REPLY OK IS ", "").Replace("\r\n", "");
        //         Console.WriteLine("Success: " + responseData);
        //         gotReply = true;
        //     }
        //     else if (responseData.Contains("REPLY NOK IS"))
        //     {
        //         responseData = responseData.Replace("REPLY NOK IS ", "").Replace("\r\n", "");
        //         Console.WriteLine("Failure: " + responseData);
        //         gotReply = true;
        //     }
        //     else if (responseData.Contains("ERR FROM"))
        //     {
        //         responseData = responseData.Replace(" IS ", ": ").Replace("\r\n", "");
        //         Console.WriteLine(responseData);
        //         gotReply = true;

        //     }
        //     else if (responseData.Contains("BYE"))
        //     {
        //         stream.Close();
        //         client.Close();
        //         Environment.Exit(0);
        //     }
        //     else if (responseData.Contains("MSG FROM"))
        //     {
        //         Console.WriteLine(responseData.Replace("MSG FROM ", "").Replace(" IS ", ": ").Replace("\r\n", "").Trim());

        //     }
        //     else
        //     {
        //         Console.WriteLine("Failure: " + responseData);
        //         gotReply = true;
        //     }
        //}
    }



    public override void HandleMsg(string messageContents)
    {
        string message = string.Format("MSG FROM {0} IS {1}\r\n", displayName, messageContents);
        byte[] data = Encoding.ASCII.GetBytes(message);
        stream.Write(data, 0, data.Length);
        //wait on reply from server
    }



    public override void HandleBye()
    {
        string message = "BYE\r\n";
        byte[] data = Encoding.ASCII.GetBytes(message);
        stream.Write(data, 0, data.Length);
    }

    public override void Close()
    {
        stream.Close();
        client.Close();
    }
}