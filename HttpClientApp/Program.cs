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
                            string request = await ReadHttpMessageAsync(tunnelStream);
                            if (string.IsNullOrEmpty(request)) break; // Connection closed

                            Console.WriteLine($"Received request:\n{request}");

                            string[] requestLines = request.Split('\n');
                            string[] requestParts = requestLines[0].Split(' ');
                            string method = requestParts[0];
                            string path = requestParts[1];

                            await ProcessRequestAsync(method, path, tunnelStream);
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

    static async Task ProcessRequestAsync(string method, string path, NetworkStream tunnelStream)
    {
        try
        {
            HttpRequestMessage httpRequest = new HttpRequestMessage(new HttpMethod(method), LocalAddress + path);
            HttpResponseMessage response = await httpClient.SendAsync(httpRequest);

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

            string content = await response.Content.ReadAsStringAsync();
            responseBuilder.Append(content);

            string fullResponse = responseBuilder.ToString();
            byte[] responseBytes = Encoding.UTF8.GetBytes(fullResponse);
            await tunnelStream.WriteAsync(responseBytes, 0, responseBytes.Length);
            await tunnelStream.FlushAsync();

            Console.WriteLine($"Sent response for {path}");
            Console.WriteLine($"Response:\n{fullResponse}");
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

    static async Task<string> ReadHttpMessageAsync(NetworkStream stream)
    {
        using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
        {
            StringBuilder message = new StringBuilder();
            string line;
            int contentLength = 0;
            bool isChunked = false;

            // Read headers
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
            {
                message.AppendLine(line);
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(line.Split(':')[1].Trim(), out int length))
                    {
                        contentLength = length;
                    }
                }
                else if (line.StartsWith("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase))
                {
                    isChunked = true;
                }
            }
            message.AppendLine(); // Add empty line after headers

            // Read body
            if (contentLength > 0)
            {
                char[] buffer = new char[contentLength];
                await reader.ReadBlockAsync(buffer, 0, contentLength);
                message.Append(buffer);
            }
            else if (isChunked)
            {
                while (true)
                {
                    string chunkSizeLine = await reader.ReadLineAsync();
                    if (!int.TryParse(chunkSizeLine, System.Globalization.NumberStyles.HexNumber, null, out int chunkSize))
                    {
                        break;
                    }
                    if (chunkSize == 0)
                    {
                        break;
                    }
                    char[] buffer = new char[chunkSize];
                    await reader.ReadBlockAsync(buffer, 0, chunkSize);
                    message.Append(buffer);
                    await reader.ReadLineAsync(); // Read the CRLF after the chunk
                }
            }

            return message.ToString();
        }
    }
}