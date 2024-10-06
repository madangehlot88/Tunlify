using System;
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

        while (true)
        {
            TcpClient tunnelClient = await tunnelListener.AcceptTcpClientAsync();
            Console.WriteLine("Tunnel client connected.");

            _ = HandleTunnelClientAsync(tunnelClient);
        }
    }

    static async Task HandleTunnelClientAsync(TcpClient tunnelClient)
    {
        try
        {
            using var tunnelStream = tunnelClient.GetStream();

            if (httpListener == null)
            {
                httpListener = new TcpListener(IPAddress.Parse(ServerIP), HttpPort);
                httpListener.Start();
                Console.WriteLine($"HTTP listener started on {ServerIP}:{HttpPort}");
            }

            while (true)
            {
                TcpClient httpClient = await httpListener.AcceptTcpClientAsync();
                _ = HandleHttpRequestAsync(httpClient, tunnelStream);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling tunnel client: {ex.Message}");
        }
        finally
        {
            tunnelClient.Close();
        }
    }

    static async Task HandleHttpRequestAsync(TcpClient httpClient, NetworkStream tunnelStream)
    {
        try
        {
            Console.WriteLine("Received HTTP request");
            using var httpStream = httpClient.GetStream();

            // Read the HTTP request
            byte[] buffer = new byte[4096];
            int bytesRead = await httpStream.ReadAsync(buffer, 0, buffer.Length);
            string request = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.WriteLine($"Received request:\n{request}");

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

            httpClient.Close();
            Console.WriteLine("HTTP request handled successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling HTTP request: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}