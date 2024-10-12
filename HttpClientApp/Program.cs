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
                            byte[] requestLengthBytes = new byte[4];
                            await tunnelStream.ReadAsync(requestLengthBytes, 0, 4);
                            int requestLength = BitConverter.ToInt32(requestLengthBytes, 0);

                            byte[] requestBuffer = new byte[requestLength];
                            await tunnelStream.ReadAsync(requestBuffer, 0, requestLength);
                            string request = Encoding.ASCII.GetString(requestBuffer);

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

            using (MemoryStream ms = new MemoryStream())
            {
                // Write status line
                byte[] statusLine = Encoding.ASCII.GetBytes($"HTTP/{response.Version} {(int)response.StatusCode} {response.ReasonPhrase}\r\n");
                ms.Write(statusLine, 0, statusLine.Length);

                // Write headers
                foreach (var header in response.Headers)
                {
                    byte[] headerLine = Encoding.ASCII.GetBytes($"{header.Key}: {string.Join(", ", header.Value)}\r\n");
                    ms.Write(headerLine, 0, headerLine.Length);
                }
                foreach (var header in response.Content.Headers)
                {
                    byte[] headerLine = Encoding.ASCII.GetBytes($"{header.Key}: {string.Join(", ", header.Value)}\r\n");
                    ms.Write(headerLine, 0, headerLine.Length);
                }

                // Write empty line to separate headers from body
                ms.Write(new byte[] { (byte)'\r', (byte)'\n' }, 0, 2);

                // Write body
                byte[] body = await response.Content.ReadAsByteArrayAsync();
                ms.Write(body, 0, body.Length);

                // Get the full response
                byte[] fullResponse = ms.ToArray();

                // Send the response length first
                byte[] responseLengthBytes = BitConverter.GetBytes(fullResponse.Length);
                await tunnelStream.WriteAsync(responseLengthBytes, 0, 4);

                // Send the complete response
                await tunnelStream.WriteAsync(fullResponse, 0, fullResponse.Length);
                await tunnelStream.FlushAsync();

                Console.WriteLine($"Sent response for {path}");
                Console.WriteLine($"Response length: {fullResponse.Length} bytes");
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error forwarding request: {ex.Message}");
            string notFoundResponse = "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\n\r\n";
            byte[] responseBytes = Encoding.ASCII.GetBytes(notFoundResponse);
            byte[] responseLengthBytes = BitConverter.GetBytes(responseBytes.Length);
            await tunnelStream.WriteAsync(responseLengthBytes, 0, 4);
            await tunnelStream.WriteAsync(responseBytes, 0, responseBytes.Length);
            await tunnelStream.FlushAsync();
            Console.WriteLine($"Sent 404 response for {path}");
        }
    }
}