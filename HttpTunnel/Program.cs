using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

class HttpTunnelServer
{
    private static int TunnelPort = 3742;
    private static int HttpPort = 3743;
    private static string ServerIP = "0.0.0.0"; // Listen on all available interfaces
    private static TcpListener httpListener;
    private static ConcurrentQueue<TcpClient> tunnelClients = new ConcurrentQueue<TcpClient>();

    static async Task Main(string[] args)
    {
        if (args.Length > 0 && int.TryParse(args[0], out int port))
        {
            TunnelPort = port;
            HttpPort = port + 1;
        }

        var tunnelListener = new TcpListener(IPAddress.Parse(ServerIP), TunnelPort);
        tunnelListener.Start();
        Console.WriteLine($"Tunnel listener started on {ServerIP}:{TunnelPort}");

        httpListener = new TcpListener(IPAddress.Parse(ServerIP), HttpPort);
        httpListener.Start();
        Console.WriteLine($"HTTP listener started on {ServerIP}:{HttpPort}");

        _ = AcceptHttpClientsAsync();

        while (true)
        {
            TcpClient tunnelClient = await tunnelListener.AcceptTcpClientAsync();
            Console.WriteLine($"Tunnel client connected from {((IPEndPoint)tunnelClient.Client.RemoteEndPoint).Address}");
            tunnelClients.Enqueue(tunnelClient);
        }
    }

    static async Task AcceptHttpClientsAsync()
    {
        while (true)
        {
            TcpClient httpClient = await httpListener.AcceptTcpClientAsync();
            _ = HandleHttpRequestAsync(httpClient);
        }
    }

    static async Task HandleHttpRequestAsync(TcpClient httpClient)
    {
        try
        {
            Console.WriteLine($"Received HTTP request from {((IPEndPoint)httpClient.Client.RemoteEndPoint).Address}");
            using var httpStream = httpClient.GetStream();

            // Read the HTTP request
            byte[] buffer = new byte[4096];
            int bytesRead = await httpStream.ReadAsync(buffer, 0, buffer.Length);
            string request = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.WriteLine($"Received request:\n{request}");

            if (tunnelClients.TryDequeue(out TcpClient tunnelClient))
            {
                using var tunnelStream = tunnelClient.GetStream();

                // Forward the request to the tunnel
                await tunnelStream.WriteAsync(buffer, 0, bytesRead);
                Console.WriteLine("Forwarded request to tunnel");

                // Read the response from the tunnel
                using var ms = new MemoryStream();
                bytesRead = await tunnelStream.ReadAsync(buffer, 0, buffer.Length);
                Console.WriteLine($"Received {bytesRead} bytes from tunnel");
                while (bytesRead > 0)
                {
                    ms.Write(buffer, 0, bytesRead);
                    if (tunnelStream.DataAvailable)
                    {
                        bytesRead = await tunnelStream.ReadAsync(buffer, 0, buffer.Length);
                        Console.WriteLine($"Received additional {bytesRead} bytes from tunnel");
                    }
                    else
                    {
                        break;
                    }
                }

                // Forward the response to the HTTP client
                byte[] response = ms.ToArray();
                Console.WriteLine($"Forwarding {response.Length} bytes to HTTP client");
                await httpStream.WriteAsync(response, 0, response.Length);

                Console.WriteLine("HTTP request handled successfully");
            }
            else
            {
                Console.WriteLine("No available tunnel clients to handle the request");
                string errorResponse = "HTTP/1.1 503 Service Unavailable\r\nContent-Length: 24\r\n\r\nNo tunnel client available";
                byte[] errorBytes = Encoding.ASCII.GetBytes(errorResponse);
                await httpStream.WriteAsync(errorBytes, 0, errorBytes.Length);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling HTTP request: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            httpClient.Close();
        }
    }
}