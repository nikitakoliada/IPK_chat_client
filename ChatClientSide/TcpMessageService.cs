using System.Net;
using System.Net.Sockets;
using System.Text;

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
        // th is this lol
        var parts = responseData.Split('\n');
        foreach (var response in parts)
        {
            if (response.Contains("MSG"))
            {
                Console.WriteLine(response.Replace("MSG FROM ", "").Replace(" IS ", ": ").Replace("\r\n", "").Trim());
            }
            else if (responseData.Contains("REPLY OK IS"))
            {
                responseData = responseData.Replace("REPLY OK IS ", "").Replace("\r\n", "");
                // if (responseData.Contains("successfully joined"))
                Console.WriteLine("Success: " + responseData);
            }
            else if (responseData.Contains("REPLY NOK IS"))
            {
                responseData = responseData.Replace("REPLY NOK IS ", "").Replace("\r\n", "");
                Console.WriteLine("Failure: " + responseData);
            }
            else if (response.Contains("ERR FROM"))
            {
                responseData = responseData.Replace(" IS ", ": ").Replace("\r\n", "");
                stream.Close();
                client.Close();
                Environment.Exit(0);
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
        int bytes = stream.Read(responseBytes, 0, responseBytes.Length);
        string responseData = Encoding.ASCII.GetString(responseBytes, 0, bytes);
        HandleResponse(message);
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