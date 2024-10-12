using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Text;
using System.IO;

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

        // Send the response back through the tunnel
        using (MemoryStream ms = new MemoryStream())
        {
            // Write status line
            byte[] statusLine = Encoding.ASCII.GetBytes($"HTTP/{response.Version} {(int)response.StatusCode} {response.ReasonPhrase}\r\n");
            await ms.WriteAsync(statusLine, 0, statusLine.Length);

            // Write headers
            foreach (var header in response.Headers)
            {
                byte[] headerLine = Encoding.ASCII.GetBytes($"{header.Key}: {string.Join(", ", header.Value)}\r\n");
                await ms.WriteAsync(headerLine, 0, headerLine.Length);
            }
            foreach (var header in response.Content.Headers)
            {
                byte[] headerLine = Encoding.ASCII.GetBytes($"{header.Key}: {string.Join(", ", header.Value)}\r\n");
                await ms.WriteAsync(headerLine, 0, headerLine.Length);
            }

            // Write empty line to separate headers from body
            await ms.WriteAsync(new byte[] { (byte)'\r', (byte)'\n' }, 0, 2);

            // Write body
            await response.Content.CopyToAsync(ms);

            // Send the complete response
            ms.Position = 0;
            await ms.CopyToAsync(tunnelStream);
            await tunnelStream.FlushAsync();
        }

        Console.WriteLine($"Proxied request for {path}");
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
                await ms.WriteAsync(buffer, 0, bytesRead);
            } while (stream.DataAvailable);

            return Encoding.ASCII.GetString(ms.ToArray());
        }
    }
}