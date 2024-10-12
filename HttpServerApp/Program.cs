using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Text;

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
                // Read request from public client
                string request = await ReadHttpMessageAsync(publicStream);

                // Forward request to tunnel client
                byte[] requestBytes = Encoding.ASCII.GetBytes(request);
                await tunnelStream.WriteAsync(requestBytes, 0, requestBytes.Length);

                // Read response from tunnel client
                string response = await ReadHttpMessageAsync(tunnelStream);

                // Forward response to public client
                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                await publicStream.WriteAsync(responseBytes, 0, responseBytes.Length);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling request: {ex.Message}");
        }
    }

    static async Task<string> ReadHttpMessageAsync(NetworkStream stream)
    {
        byte[] buffer = new byte[8192];
        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        return Encoding.ASCII.GetString(buffer, 0, bytesRead);
    }
}