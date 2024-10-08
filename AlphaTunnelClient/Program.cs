﻿using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

class ImprovedTcpTunnelClient
{
    private static readonly X509Certificate2 ClientCertificate = new X509Certificate2("client.pfx", "1234");

    static async Task Main(string[] args)
    {
        if (args.Length != 4)
        {
            Console.WriteLine("Usage: TcpTunnelClient <ServerIp> <ServerPort> <LocalIp> <LocalPort>");
            return;
        }

        string serverIp = args[0];
        int serverPort = int.Parse(args[1]);
        string localIp = args[2];
        int localPort = int.Parse(args[3]);

        while (true)
        {
            try
            {
                await ConnectAndForwardAsync(serverIp, serverPort, localIp, localPort);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(5)); // Wait before retrying
            }
        }
    }

    static async Task ConnectAndForwardAsync(string serverIp, int serverPort, string localIp, int localPort)
    {
        using (TcpClient serverClient = new TcpClient())
        using (TcpClient localClient = new TcpClient())
        {
            Console.WriteLine($"Connecting to server at {serverIp}:{serverPort}...");
            await serverClient.ConnectAsync(serverIp, serverPort);

            using (SslStream sslStream = new SslStream(serverClient.GetStream(), false, ValidateServerCertificate, null))
            {
                await sslStream.AuthenticateAsClientAsync(serverIp, new X509CertificateCollection { ClientCertificate }, System.Security.Authentication.SslProtocols.Tls12, false);

                // Send the key
                byte[] key = new byte[] { 0, 8, 0, 0, 0, 34, 77, 0, 0, 0 };
                await sslStream.WriteAsync(key, 0, key.Length);

                // Receive the IP address and port (20 bytes)
                byte[] response = new byte[20];
                await sslStream.ReadAsync(response, 0, response.Length);

                // Extract server port from the response
                int receivedServerPort = BitConverter.ToInt32(response, 16);

                Console.WriteLine("Connected to server and key verified.");
                Console.WriteLine($"Server is listening on port: {receivedServerPort}");

                Console.WriteLine($"Connecting to local endpoint at {localIp}:{localPort}...");
                await localClient.ConnectAsync(localIp, localPort);
                Console.WriteLine("Connected to local endpoint.");

                using (NetworkStream localStream = localClient.GetStream())
                {
                    Console.WriteLine("Forwarding traffic...");
                    using (var cts = new CancellationTokenSource())
                    {
                        Task serverToLocal = ForwardTrafficAsync(sslStream, localStream, "Server -> Local", cts.Token);
                        Task localToServer = ForwardTrafficAsync(localStream, sslStream, "Local -> Server", cts.Token);

                        await Task.WhenAny(serverToLocal, localToServer);
                        cts.Cancel(); // Cancel the other task when one completes
                        await Task.WhenAll(serverToLocal, localToServer); // Wait for both tasks to complete
                    }
                }
            }
        }
    }

    static async Task ForwardTrafficAsync(Stream source, Stream destination, string direction, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[8192];
        try
        {
            int bytesRead;
            while (!cancellationToken.IsCancellationRequested &&
                   (bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                await destination.FlushAsync(cancellationToken);
                Console.WriteLine($"{direction}: Forwarded {bytesRead} bytes");
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in {direction}: {ex.Message}");
        }
    }

    private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        Console.WriteLine($"Validating server certificate. Errors: {sslPolicyErrors}");

        // For testing purposes, we'll ignore RemoteCertificateNameMismatch and RemoteCertificateChainErrors
        if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateNameMismatch ||
            sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors ||
            sslPolicyErrors == (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors))
        {
            Console.WriteLine("Ignoring name mismatch and/or chain validation errors for self-signed certificate.");
            return true;
        }

        // If there are any other SSL policy errors, reject the certificate
        if (sslPolicyErrors != SslPolicyErrors.None)
        {
            Console.WriteLine($"Certificate validation failed due to {sslPolicyErrors}");
            return false;
        }

        // If we get here, the certificate is valid
        Console.WriteLine("Server certificate validated successfully.");
        return true;
    }
}