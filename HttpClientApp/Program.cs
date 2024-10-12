using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

class TunnelClient
{
    static readonly string LocalAddress = "http://localhost:5000"; // Your local web server address
    static readonly HttpClient httpClient = new HttpClient();

    static async Task Main(string[] args)
    {
        string serverAddress = "167.71.227.50";
        int tunnelPort = 8001;

        while (true)
        {
            try
            {
                using (TcpClient tunnelClient = new TcpClient())
                {
                    await tunnelClient.ConnectAsync(serverAddress, tunnelPort);
                    Console.WriteLine("Connected to tunnel server");

                    using (NetworkStream tunnelStream = tunnelClient.GetStream())
                    {
                        while (true)
                        {
                            await ProxyRequest(tunnelStream);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine("Reconnecting in 5 seconds...");
                await Task.Delay(5000);
            }
        }
    }

    static async Task ProxyRequest(NetworkStream tunnelStream)
    {
        // Read the request from the tunnel
        byte[] buffer = new byte[8192];
        int bytesRead = await tunnelStream.ReadAsync(buffer, 0, buffer.Length);
        if (bytesRead == 0) return; // Connection closed

        string request = System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead);
        Console.WriteLine($"Received request:\n{request}");

        // Extract the path from the request
        string[] requestLines = request.Split('\n');
        string[] requestParts = requestLines[0].Split(' ');
        string path = requestParts[1];

        // Forward the request to the local server
        HttpResponseMessage response = await httpClient.GetAsync(LocalAddress + path);

        // Send the response back through the tunnel
        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();
        await tunnelStream.WriteAsync(responseBytes, 0, responseBytes.Length);

        Console.WriteLine($"Proxied request for {path}");
    }
}