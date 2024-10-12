using System.Net.Sockets;
using System.Text;

static async Task Main(string[] args)
{
    string serverAddress = "167.71.227.50";
    int tunnelPort = 8001;
    string localAddress = "http://localhost:5000"; // Your local web server address

    using (TcpClient tunnelClient = new TcpClient())
    using (HttpClient httpClient = new HttpClient())
    {
        await tunnelClient.ConnectAsync(serverAddress, tunnelPort);
        Console.WriteLine("Connected to tunnel server");

        using (NetworkStream tunnelStream = tunnelClient.GetStream())
        {
            byte[] buffer = new byte[4096];
            int bytesRead;

            while (true)
            {
                // Read the request from the tunnel server
                bytesRead = await tunnelStream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break; // Connection closed

                string request = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Received request:\n{request}");

                // Extract the path from the request
                string[] requestLines = request.Split('\n');
                string[] requestParts = requestLines[0].Split(' ');
                string path = requestParts[1];

                // Forward the request to the local web server
                HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Get, localAddress + path);
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
                fullResponse.Append(await response.Content.ReadAsStringAsync());

                // Send the full response back through the tunnel
                byte[] responseBytes = Encoding.ASCII.GetBytes(fullResponse.ToString());
                await tunnelStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                Console.WriteLine($"Sent response:\n{fullResponse}");
            }
        }
    }
}