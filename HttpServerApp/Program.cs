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

        // Wait for a tunnel connection
        TcpClient tunnelClient = await tunnelListener.AcceptTcpClientAsync();
        Console.WriteLine("Tunnel client connected");

        while (true)
        {
            TcpClient publicClient = await publicListener.AcceptTcpClientAsync();
            _ = HandleRequestAsync(publicClient, tunnelClient);
        }
    }

    static async Task HandleRequestAsync(TcpClient publicClient, TcpClient tunnelClient)
    {
        using (publicClient)
        using (NetworkStream publicStream = publicClient.GetStream())
        using (NetworkStream tunnelStream = tunnelClient.GetStream())
        {
            byte[] buffer = new byte[4096];
            int bytesRead;

            // Read the request from the public client
            bytesRead = await publicStream.ReadAsync(buffer, 0, buffer.Length);
            string request = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.WriteLine($"Received request:\n{request}");

            // Forward the request to the tunnel client
            await tunnelStream.WriteAsync(buffer, 0, bytesRead);

            // Read the response from the tunnel client
            bytesRead = await tunnelStream.ReadAsync(buffer, 0, buffer.Length);

            // Forward the response to the public client
            await publicStream.WriteAsync(buffer, 0, bytesRead);
            string response = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.WriteLine($"Sent response:\n{response}");
        }
    }
}