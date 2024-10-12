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
                // Read request from public client
                byte[] requestBuffer = new byte[4096];
                int bytesRead = await publicStream.ReadAsync(requestBuffer, 0, requestBuffer.Length);
                string request = Encoding.ASCII.GetString(requestBuffer, 0, bytesRead);
                Console.WriteLine($"Received request:\n{request}");

                // Send request length to tunnel client
                byte[] requestLengthBytes = BitConverter.GetBytes(bytesRead);
                await tunnelStream.WriteAsync(requestLengthBytes, 0, 4);

                // Forward request to tunnel client
                await tunnelStream.WriteAsync(requestBuffer, 0, bytesRead);
                await tunnelStream.FlushAsync();

                // Read response length from tunnel client
                byte[] responseLengthBytes = new byte[4];
                await tunnelStream.ReadAsync(responseLengthBytes, 0, 4);
                int responseLength = BitConverter.ToInt32(responseLengthBytes, 0);

                // Read response from tunnel client
                byte[] responseBuffer = new byte[responseLength];
                int totalBytesRead = 0;
                while (totalBytesRead < responseLength)
                {
                    int bytesRemaining = responseLength - totalBytesRead;
                    int bytesToRead = Math.Min(bytesRemaining, 4096);
                    int bytesReadThisTime = await tunnelStream.ReadAsync(responseBuffer, totalBytesRead, bytesToRead);
                    totalBytesRead += bytesReadThisTime;
                }

                Console.WriteLine($"Received response from tunnel client: {responseLength} bytes");

                // Forward response to public client
                await publicStream.WriteAsync(responseBuffer, 0, responseLength);
                await publicStream.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling request: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}