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
                string request = await ReadHttpMessageAsync(publicStream);
                Console.WriteLine($"Received request:\n{request}");

                byte[] requestBytes = Encoding.ASCII.GetBytes(request);
                await tunnelStream.WriteAsync(requestBytes, 0, requestBytes.Length);
                await tunnelStream.FlushAsync();

                string response = await ReadHttpMessageAsync(tunnelStream);
                Console.WriteLine($"Received response from tunnel client:\n{response}");

                byte[] responseBytes = Encoding.ASCII.GetBytes(response);
                await publicStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                await publicStream.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling request: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
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