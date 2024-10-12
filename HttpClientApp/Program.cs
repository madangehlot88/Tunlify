using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Text;

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
        string request = await ReadHttpMessageAsync(tunnelStream);
        if (string.IsNullOrEmpty(request)) return; // Connection closed

        Console.WriteLine($"Received request:\n{request}");

        // Extract the path from the request
        string[] requestLines = request.Split('\n');
        string[] requestParts = requestLines[0].Split(' ');
        string method = requestParts[0];
        string path = requestParts[1];

        // Forward the request to the local server
        HttpRequestMessage httpRequest = new HttpRequestMessage(new HttpMethod(method), LocalAddress + path);
        HttpResponseMessage response = await httpClient.SendAsync(httpRequest);

        // Construct the response
        StringBuilder responseBuilder = new StringBuilder();
        responseBuilder.AppendLine($"HTTP/{response.Version} {(int)response.StatusCode} {response.ReasonPhrase}");
        foreach (var header in response.Headers)
        {
            responseBuilder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
        }
        foreach (var header in response.Content.Headers)
        {
            responseBuilder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
        }
        responseBuilder.AppendLine();
        responseBuilder.Append(await response.Content.ReadAsStringAsync());

        // Send the response back through the tunnel
        byte[] responseBytes = Encoding.UTF8.GetBytes(responseBuilder.ToString());
        await tunnelStream.WriteAsync(responseBytes, 0, responseBytes.Length);

        Console.WriteLine($"Proxied request for {path}");
    }

    static async Task<string> ReadHttpMessageAsync(NetworkStream stream)
    {
        byte[] buffer = new byte[8192];
        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        return Encoding.ASCII.GetString(buffer, 0, bytesRead);
    }
}