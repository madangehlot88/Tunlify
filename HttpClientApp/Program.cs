using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

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
                                StringBuilder fullResponse = new StringBuilder();
                                fullResponse.AppendLine($"HTTP/{response.Version} {(int)response.StatusCode} {response.ReasonPhrase}");
                                foreach (var header in response.Headers)
                                {
                                    fullResponse.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
                                }
                                foreach (var header in response.Content.Headers)
                                {
                                    fullResponse.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
                                }
                                fullResponse.AppendLine();
                                string content = await response.Content.ReadAsStringAsync();
                                fullResponse.AppendLine($"Content-Length: {Encoding.UTF8.GetByteCount(content)}");
                                fullResponse.AppendLine();
                                fullResponse.Append(content);

                                byte[] responseBytes = Encoding.UTF8.GetBytes(fullResponse.ToString());
                                await WriteFullMessageAsync(tunnelStream, responseBytes);
                                Console.WriteLine($"Sent response for {path}");
                                Console.WriteLine($"Response:\n{fullResponse}");
                            }
                            catch (HttpRequestException)
                            {
                                string notFoundResponse = "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\n\r\n";
                                byte[] responseBytes = Encoding.UTF8.GetBytes(notFoundResponse);
                                await WriteFullMessageAsync(tunnelStream, responseBytes);
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
        using (var ms = new System.IO.MemoryStream())
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

    static async Task WriteFullMessageAsync(NetworkStream stream, byte[] message)
    {
        await stream.WriteAsync(message, 0, message.Length);
        await stream.FlushAsync();
    }
}