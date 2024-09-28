﻿using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;

class ImprovedTcpTunnelServer
{
    private static int TunnelPort = 5900;
    private static int ServerPort;
    private static string LocalIP;
    private static int LocalPort;
    private static bool UseHttp = false;
    private static bool AllowAllCertificates = false;
    private static List<string> AllowedThumbprints = new List<string>();

    private static readonly X509Certificate2 ServerCertificate = new X509Certificate2("server.pfx", "1234");

    static async Task Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: dotnet run <ServerPort> [--http <LocalIP> <LocalPort>] [--allow-all-certs] [--allow-thumbprint <thumbprint>]");
            return;
        }

        if (!int.TryParse(args[0], out ServerPort))
        {
            Console.WriteLine("Invalid ServerPort. Please provide a valid port number.");
            return;
        }

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--http":
                    if (i + 2 < args.Length)
                    {
                        UseHttp = true;
                        LocalIP = args[++i];
                        if (!int.TryParse(args[++i], out LocalPort))
                        {
                            Console.WriteLine("Invalid LocalPort. Please provide a valid port number.");
                            return;
                        }
                        Console.WriteLine($"HTTP mode enabled, forwarding to {LocalIP}:{LocalPort}");
                    }
                    else
                    {
                        Console.WriteLine("--http requires LocalIP and LocalPort arguments.");
                        return;
                    }
                    break;
                case "--allow-all-certs":
                    AllowAllCertificates = true;
                    Console.WriteLine("Allowing all client certificates");
                    break;
                case "--allow-thumbprint":
                    if (i + 1 < args.Length)
                    {
                        AllowedThumbprints.Add(args[++i].ToUpper());
                        Console.WriteLine($"Added allowed thumbprint: {args[i]}");
                    }
                    break;
            }
        }

        try
        {
            var listener = new TcpListener(IPAddress.Any, UseHttp ? ServerPort : TunnelPort);
            listener.Start();
            Console.WriteLine($"Server listening on port {(UseHttp ? ServerPort : TunnelPort)}");
            if (!UseHttp)
                Console.WriteLine($"Forwarding to local service on port {ServerPort}");

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
        }
    }

    static int GetAvailablePort()
    {
        TcpListener l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    static async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                using (SslStream sslStream = new SslStream(client.GetStream(), false, ValidateClientCertificate, null, EncryptionPolicy.AllowNoEncryption))
                {
                    await sslStream.AuthenticateAsServerAsync(ServerCertificate, clientCertificateRequired: !UseHttp, SslProtocols.Tls12 | SslProtocols.Tls13, checkCertificateRevocation: true);

                    Console.WriteLine($"Cipher: {sslStream.CipherAlgorithm} strength {sslStream.CipherStrength}");
                    Console.WriteLine($"Hash: {sslStream.HashAlgorithm} strength {sslStream.HashStrength}");
                    Console.WriteLine($"Key exchange: {sslStream.KeyExchangeAlgorithm} strength {sslStream.KeyExchangeStrength}");
                    Console.WriteLine($"Protocol: {sslStream.SslProtocol}");

                    if (UseHttp)
                    {
                        await HandleHttpRequest(sslStream);
                    }
                    else
                    {
                        await HandleOriginalProtocol(sslStream, client);
                    }
                }
            }
            catch (AuthenticationException ex)
            {
                Console.WriteLine($"SSL/TLS authentication failed: {ex.Message}");
                Console.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
                Console.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
            }
        }
    }

    static async Task HandleHttpRequest(SslStream sslStream)
    {
        using (var client = new TcpClient(LocalIP, LocalPort))
        using (var stream = client.GetStream())
        {
            await Task.WhenAny(
                sslStream.CopyToAsync(stream),
                stream.CopyToAsync(sslStream)
            );
        }
    }

    static async Task HandleOriginalProtocol(SslStream sslStream, TcpClient client)
    {
        // Read the key
        byte[] keyBuffer = new byte[10];
        await sslStream.ReadAsync(keyBuffer, 0, keyBuffer.Length);

        if (!VerifyKey(keyBuffer))
        {
            Console.WriteLine("Invalid key received. Closing connection.");
            return;
        }

        // Send back the client's IP address and the tunnel port
        byte[] ipBytes = ((IPEndPoint)client.Client.RemoteEndPoint).Address.GetAddressBytes();
        byte[] portBytes = BitConverter.GetBytes(TunnelPort);
        byte[] response = new byte[20]; // 16 bytes for IP, 4 bytes for port
        Array.Copy(ipBytes, 0, response, 0, ipBytes.Length);
        Array.Copy(portBytes, 0, response, 16, 4);
        await sslStream.WriteAsync(response, 0, response.Length);

        Console.WriteLine($"Client connected: {((IPEndPoint)client.Client.RemoteEndPoint).Address}");

        // Wait for a connection on the ServerPort
        TcpListener serverListener = new TcpListener(IPAddress.Any, ServerPort);
        serverListener.Start();
        Console.WriteLine($"Waiting for local service connection on port {ServerPort}");

        using (TcpClient serverClient = await serverListener.AcceptTcpClientAsync())
        using (NetworkStream serverStream = serverClient.GetStream())
        {
            Console.WriteLine($"Local service connected on port {ServerPort}. Forwarding traffic...");

            // Forward traffic in both directions
            Task clientToServer = ForwardTrafficAsync(sslStream, serverStream, "Client -> Server");
            Task serverToClient = ForwardTrafficAsync(serverStream, sslStream, "Server -> Client");

            await Task.WhenAny(clientToServer, serverToClient);
        }

        serverListener.Stop();
    }

    static async Task ForwardTrafficAsync(Stream source, Stream destination, string direction)
    {
        byte[] buffer = new byte[8192];
        try
        {
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead);
                await destination.FlushAsync();
                Console.WriteLine($"{direction}: Forwarded {bytesRead} bytes");
            }
        }
        catch (IOException)
        {
            Console.WriteLine($"{direction}: Connection closed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in {direction}: {ex.Message}");
        }
    }

    private static bool ValidateClientCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        if (AllowAllCertificates)
        {
            Console.WriteLine("Accepting all client certificates as per configuration.");
            return true;
        }

        if (certificate == null)
        {
            Console.WriteLine("Client didn't provide a certificate.");
            return false;
        }

        X509Certificate2 cert2 = new X509Certificate2(certificate);
        string thumbprint = cert2.Thumbprint;

        Console.WriteLine($"Received client certificate with thumbprint: {thumbprint}");
        Console.WriteLine($"Certificate subject: {cert2.Subject}");
        Console.WriteLine($"Certificate issuer: {cert2.Issuer}");
        Console.WriteLine($"Certificate valid from: {cert2.NotBefore} to {cert2.NotAfter}");

        if (AllowedThumbprints.Contains(thumbprint))
        {
            Console.WriteLine("Client certificate is in the list of allowed certificates.");
            return true;
        }

        Console.WriteLine($"Client certificate is not in the list of allowed certificates. Thumbprint: {thumbprint}");
        Console.WriteLine($"SSL Policy Errors: {sslPolicyErrors}");
        return false;
    }

    private static X509Certificate SelectServerCertificate(object sender, string hostName, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
    {
        // Here you can implement logic to select the appropriate server certificate
        // For now, we're just returning the single server certificate we loaded
        return ServerCertificate;
    }

    private static bool VerifyKey(byte[] key)
    {
        byte[] expectedKey = new byte[] { 0, 8, 0, 0, 0, 34, 77, 0, 0, 0 };
        return key.AsSpan().SequenceEqual(expectedKey);
    }
}