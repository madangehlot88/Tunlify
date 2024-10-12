using System;
using System.Net;
using System.Net.Sockets;
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
            await tunnelStream.CopyToAsync(publicStream);
            await publicStream.CopyToAsync(tunnelStream);
        }
    }
}