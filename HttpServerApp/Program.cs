using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

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
                    _ = HandleRequestAsync(publicClient, tunnelStream);
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
                // Read the request from the public client
                byte[] requestBuffer = await ReadFullMessageAsync(publicStream);
                string request = Encoding.ASCII.GetString(requestBuffer);
                Console.WriteLine($"Received request:\n{request}");

                // Forward the request to the tunnel client
                await WriteFullMessageAsync(tunnelStream, requestBuffer);

                // Read the response from the tunnel client
                byte[] responseBuffer = await ReadFullMessageAsync(tunnelStream);
                string response = Encoding.ASCII.GetString(responseBuffer);
                Console.WriteLine($"Received response from local server:\n{response}");

                // Forward the response to the public client
                await WriteFullMessageAsync(publicStream, responseBuffer);
                Console.WriteLine($"Sent response to client");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling request: {ex.Message}");
        }
    }

    static async Task<byte[]> ReadFullMessageAsync(NetworkStream stream)
    {
        byte[] buffer = new byte[4096];
        using (var ms = new System.IO.MemoryStream())
        {
            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;
                await ms.WriteAsync(buffer, 0, bytesRead);
                if (bytesRead < buffer.Length) break;
            }
            return ms.ToArray();
        }
    }

    static async Task WriteFullMessageAsync(NetworkStream stream, byte[] message)
    {
        await stream.WriteAsync(message, 0, message.Length);
        await stream.FlushAsync();
    }
}