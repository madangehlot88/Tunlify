﻿using System;
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
        _ = AcceptTunnelClientsAsync(tunnelListener);

        await Task.Delay(-1); // Keep the application running
    }

    static async Task AcceptTunnelClientsAsync(TcpListener listener)
    {
        while (true)
        {
            TcpClient tunnelClient = await listener.AcceptTcpClientAsync();
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

            TcpClient tunnelClient = null;
            for (int i = 0; i < 3; i++) // Try up to 3 times
            {
                if (tunnelClients.TryDequeue(out tunnelClient))
                {
                    break;
                }
                await Task.Delay(1000); // Wait for 1 second before retrying
                Console.WriteLine($"Retry {i + 1} to get tunnel client");
            }

            if (tunnelClient != null)
            {
                using var tunnelStream = tunnelClient.GetStream();
                // Forward the request to the tunnel
                await WriteToStreamSafelyAsync(tunnelStream, buffer, 0, bytesRead);
                Console.WriteLine("Forwarded request to tunnel");

                // Read the response from the tunnel with a timeout
                using var ms = new MemoryStream();
                var readTask = ReadFromStreamWithTimeoutAsync(tunnelStream, ms, TimeSpan.FromSeconds(10));
                try
                {
                    await readTask;
                    byte[] response = ms.ToArray();
                    Console.WriteLine($"Forwarding {response.Length} bytes to HTTP client");
                    await WriteToStreamSafelyAsync(httpStream, response);
                    Console.WriteLine("HTTP request handled successfully");
                }
                catch (TimeoutException)
                {
                    Console.WriteLine("Timeout reading from tunnel");
                    string errorResponse = "HTTP/1.1 504 Gateway Timeout\r\nContent-Length: 21\r\n\r\nTunnel read timed out";
                    byte[] errorBytes = Encoding.ASCII.GetBytes(errorResponse);
                    await WriteToStreamSafelyAsync(httpStream, errorBytes);
                }
            }
            else
            {
                Console.WriteLine("No available tunnel clients to handle the request");
                string errorResponse = "HTTP/1.1 503 Service Unavailable\r\nContent-Length: 24\r\n\r\nNo tunnel client available";
                byte[] errorBytes = Encoding.ASCII.GetBytes(errorResponse);
                await WriteToStreamSafelyAsync(httpStream, errorBytes);
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

    static async Task ReadFromStreamWithTimeoutAsync(NetworkStream stream, MemoryStream ms, TimeSpan timeout)
    {
        byte[] buffer = new byte[4096];
        using var cts = new System.Threading.CancellationTokenSource(timeout);
        try
        {
            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                if (bytesRead == 0) break;
                ms.Write(buffer, 0, bytesRead);
                Console.WriteLine($"Received {bytesRead} bytes from tunnel");
                if (!stream.DataAvailable) break;
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException();
        }
    }

    static async Task WriteToStreamSafelyAsync(Stream stream, byte[] buffer, int offset = 0, int count = -1)
    {
        try
        {
            if (count == -1) count = buffer.Length;
            await stream.WriteAsync(buffer, offset, count);
            await stream.FlushAsync();
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Error writing to stream: {ex.Message}");
            // Handle the broken pipe or connection reset here
            // For now, we'll just log it and let the caller handle the failure
            throw;
        }
    }
}