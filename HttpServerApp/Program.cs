using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Text;
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
                await tunnelStream.FlushAsync();

                // Read response from tunnel client and forward to public client
                await CopyStreamAsync(tunnelStream, publicStream);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling request: {ex.Message}");
        }
    }

    static async Task<string> ReadHttpMessageAsync(NetworkStream stream)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            byte[] buffer = new byte[4096];
            int bytesRead;
            do
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                ms.Write(buffer, 0, bytesRead);
            } while (bytesRead == buffer.Length);

            return Encoding.ASCII.GetString(ms.ToArray());
        }
    }

    static async Task CopyStreamAsync(NetworkStream source, NetworkStream destination)
    {
        byte[] buffer = new byte[4096];
        int bytesRead;
        do
        {
            bytesRead = await source.ReadAsync(buffer, 0, buffer.Length);
            await destination.WriteAsync(buffer, 0, bytesRead);
        } while (bytesRead > 0);
        await destination.FlushAsync();
    }
}