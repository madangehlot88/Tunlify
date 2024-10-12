using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.IO;

class TunnelClient
{
    static readonly string LocalAddress = "http://localhost:5000"; // Your local web server address
    static readonly HttpClient httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(10) };

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
                            byte[] requestBuffer = await ReadFullMessageAsync(tunnelStream);
                            if (requestBuffer.Length == 0) break; // Connection closed

                            string request = Encoding.ASCII.GetString(requestBuffer);
                            Console.WriteLine($"Received request:\n{request}");

                            string[] requestLines = request.Split('\n');
                            string[] requestParts = requestLines[0].Split(' ');
                            string method = requestParts[0];
                            string path = requestParts[1];

                            try
                            {
                                // Forward the request to the local web server
                                HttpRequestMessage httpRequest = new HttpRequestMessage(new HttpMethod(method), LocalAddress + path);
                                HttpResponseMessage response = await httpClient.SendAsync(httpRequest);

                                // Construct the full HTTP response
                                StringBuilder headerBuilder = new StringBuilder();
                                headerBuilder.AppendLine($"HTTP/{response.Version} {(int)response.StatusCode} {response.ReasonPhrase}");
                                foreach (var header in response.Headers)
                                {
                                    headerBuilder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
                                }
                                foreach (var header in response.Content.Headers)
                                {
                                    if (header.Key.ToLower() != "content-length")
                                    {
                                        headerBuilder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
                                    }
                                }

                                byte[] content = await response.Content.ReadAsByteArrayAsync();
                                headerBuilder.AppendLine($"Content-Length: {content.Length}");
                                headerBuilder.AppendLine();

                                byte[] headerBytes = Encoding.ASCII.GetBytes(headerBuilder.ToString());
                                await tunnelStream.WriteAsync(headerBytes, 0, headerBytes.Length);
                                await tunnelStream.WriteAsync(content, 0, content.Length);
                                await tunnelStream.FlushAsync();

                                Console.WriteLine($"Sent response for {path}");
                                Console.WriteLine($"Response headers:\n{headerBuilder}");
                                Console.WriteLine($"Response body length: {content.Length} bytes");
                            }
                            catch (HttpRequestException ex)
                            {
                                Console.WriteLine($"Error forwarding request: {ex.Message}");
                                string notFoundResponse = "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\n\r\n";
                                byte[] responseBytes = Encoding.ASCII.GetBytes(notFoundResponse);
                                await tunnelStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                                await tunnelStream.FlushAsync();
                                Console.WriteLine($"Sent 404 response for {path}");
                            }
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

    static async Task<byte[]> ReadFullMessageAsync(NetworkStream stream)
    {
        byte[] buffer = new byte[4096];
        using (var ms = new MemoryStream())
        {
            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;
                await ms.WriteAsync(buffer, 0, bytesRead);
                if (bytesRead < buffer.Length) break;
            }
            return ms.ToArray();
        }
    }
}