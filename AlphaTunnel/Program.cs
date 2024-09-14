using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

class Server
{
    private const int ServerPort = 5900;
    private const int RdpPort = 3742;

    static async Task Main(string[] args)
    {
        TcpListener serverListener = new TcpListener(IPAddress.Any, ServerPort);

        try
        {
            serverListener.Start();
            Console.WriteLine($"Server listening on port {ServerPort}");

            while (true)
            {
                TcpClient clientConnection = await serverListener.AcceptTcpClientAsync();
                _ = HandleClientAsync(clientConnection);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            serverListener.Stop();
        }
    }

    static async Task HandleClientAsync(TcpClient clientConnection)
    {
        using (clientConnection)
        using (NetworkStream clientStream = clientConnection.GetStream())
        {
            try
            {
                // Read the key sent by the client
                byte[] keyBuffer = new byte[10];
                await clientStream.ReadAsync(keyBuffer, 0, keyBuffer.Length);

                if (!VerifyKey(keyBuffer))
                {
                    Console.WriteLine("Invalid key received. Closing connection.");
                    return;
                }

                // Send back the client's IP address
                byte[] ipBytes = ((IPEndPoint)clientConnection.Client.RemoteEndPoint).Address.GetAddressBytes();
                if (ipBytes.Length == 4)
                {
                    byte[] paddedIpBytes = new byte[16];
                    Array.Copy(ipBytes, 0, paddedIpBytes, 12, 4);
                    ipBytes = paddedIpBytes;
                }
                await clientStream.WriteAsync(ipBytes, 0, ipBytes.Length);

                Console.WriteLine($"Client connected: {((IPEndPoint)clientConnection.Client.RemoteEndPoint).Address}");

                // Wait for an RDP client to connect
                TcpListener rdpListener = new TcpListener(IPAddress.Any, RdpPort);
                rdpListener.Start();
                Console.WriteLine($"Waiting for RDP client on port {RdpPort}");

                using (TcpClient rdpClient = await rdpListener.AcceptTcpClientAsync())
                using (NetworkStream rdpStream = rdpClient.GetStream())
                {
                    Console.WriteLine("RDP client connected. Forwarding traffic.");

                    // Forward traffic in both directions
                    Task clientToRdp = ForwardTrafficAsync(clientStream, rdpStream);
                    Task rdpToClient = ForwardTrafficAsync(rdpStream, clientStream);

                    await Task.WhenAny(clientToRdp, rdpToClient);
                }

                rdpListener.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
            }
        }
    }

    static async Task ForwardTrafficAsync(NetworkStream source, NetworkStream destination)
    {
        byte[] buffer = new byte[4096];
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await destination.WriteAsync(buffer, 0, bytesRead);
            await destination.FlushAsync();
        }
    }

    static bool VerifyKey(byte[] key)
    {
        // Implement your key verification logic here
        byte[] expectedKey = new byte[] { 0, 8, 0, 0, 0, 34, 77, 0, 0, 0 };
        return key.AsSpan().SequenceEqual(expectedKey);
    }
}