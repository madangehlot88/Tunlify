using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.IO;

class TunnelServer
{
    static async Task Main(string[] args)
    {
        int port = 8000; // Port for incoming public requests
        int tunnelPort = 8001; // Port for tunnel connections from clients

        var publicListener = new TcpListener(IPAddress.Any, port);
        var tunnelListener = new TcpListener(IPAddress.Any, tunnelPort);

        publicListener.Start();
        tunnelListener.Start();

        Console.WriteLine($"Server listening for public requests on port {port}");
        Console.WriteLine($"Server listening for tunnel connections on port {tunnelPort}");

        while (true)
        {
            TcpClient tunnelClient = await tunnelListener.AcceptTcpClientAsync();
            Console.WriteLine("Tunnel client connected");

            _ = HandleTunnelClientAsync(tunnelClient, publicListener);
        }
    }

    static async Task HandleTunnelClientAsync(TcpClient tunnelClient, TcpListener publicListener)
    {
        try
        {
            using (tunnelClient)
            using (NetworkStream tunnelStream = tunnelClient.GetStream())
            {
                while (true)
                {
                    TcpClient publicClient = await publicListener.AcceptTcpClientAsync();
                    await HandleRequestAsync(publicClient, tunnelStream);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Tunnel client disconnected: {ex.Message}");
        }
    }

    static async Task HandleRequestAsync(TcpClient publicClient, NetworkStream tunnelStream)
    {
        try
        {
            using (publicClient)
            using (NetworkStream publicStream = publicClient.GetStream())
            {
                string request = await ReadHttpMessageAsync(publicStream);
                Console.WriteLine($"Received request:\n{request}");

                byte[] requestBytes = Encoding.ASCII.GetBytes(request);
                await tunnelStream.WriteAsync(requestBytes, 0, requestBytes.Length);
                await tunnelStream.FlushAsync();

                string response = await ReadHttpMessageAsync(tunnelStream);
                Console.WriteLine($"Received response from tunnel client:\n{response}");

                byte[] responseBytes = Encoding.ASCII.GetBytes(response);
                await publicStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                await publicStream.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling request: {ex.Message}");
        }
    }

    static async Task<string> ReadHttpMessageAsync(NetworkStream stream)
    {
        using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
        {
            StringBuilder message = new StringBuilder();
            string line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
            {
                message.AppendLine(line);
            }

            // Read the message body if Content-Length is present
            string headers = message.ToString();
            string contentLengthHeader = headers.Split(new[] { "Content-Length: " }, StringSplitOptions.None)[1];
            int contentLength = int.Parse(contentLengthHeader.Split('\r')[0]);

            char[] buffer = new char[contentLength];
            await reader.ReadBlockAsync(buffer, 0, contentLength);
            message.Append(buffer);

            return message.ToString();
        }
    }
}