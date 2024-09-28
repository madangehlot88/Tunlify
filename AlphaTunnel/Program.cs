﻿using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.IO;

class TcpTunnelServer
{
    private static int ServerPort;
    private static int LocalPort = 80;
    private static bool UseHttp = false;
    private static bool AllowAllCertificates = false;
    private static List<string> AllowedThumbprints = new List<string>();
    private static readonly X509Certificate2 ServerCertificate = new X509Certificate2("server.pfx", "1234");

    static async Task Main(string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out ServerPort))
        {
            Console.WriteLine("Usage: dotnet run <ServerPort> [--http] [--local-port <Port>] [--allow-all-certs] [--allow-thumbprint <thumbprint>]");
            return;
        }

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--http":
                    UseHttp = true;
                    Console.WriteLine("HTTP mode enabled");
                    break;
                case "--local-port":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int port))
                    {
                        LocalPort = port;
                        Console.WriteLine($"Local port set to: {LocalPort}");
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
            var listener = new TcpListener(IPAddress.Any, ServerPort);
            listener.Start();
            Console.WriteLine($"Server listening on port {ServerPort}");

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

    static async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                using (SslStream sslStream = new SslStream(client.GetStream(), false, ValidateClientCertificate))
                {
                    var sslServerAuthOptions = new SslServerAuthenticationOptions
                    {
                        ServerCertificate = ServerCertificate,
                        ClientCertificateRequired = !UseHttp,
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                        RemoteCertificateValidationCallback = ValidateClientCertificate
                    };

                    await sslStream.AuthenticateAsServerAsync(sslServerAuthOptions);

                    Console.WriteLine($"Client connected: {((IPEndPoint)client.Client.RemoteEndPoint).Address}");

                    if (!UseHttp)
                    {
                        await HandleOriginalProtocol(sslStream);
                    }
                    else
                    {
                        await HandleHttpMode(sslStream);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }

    static async Task HandleOriginalProtocol(SslStream sslStream)
    {
        byte[] buffer = new byte[10];
        await sslStream.ReadAsync(buffer, 0, buffer.Length);

        if (!VerifyKey(buffer))
        {
            Console.WriteLine("Invalid key received. Closing connection.");
            return;
        }

        Console.WriteLine("Valid key received.");

        int tunnelPort = 5900; // You can change this or make it dynamic
        byte[] response = new byte[20];
        Array.Copy(BitConverter.GetBytes(tunnelPort), 0, response, 16, 4);
        await sslStream.WriteAsync(response, 0, response.Length);

        Console.WriteLine($"Sent tunnel port: {tunnelPort}");

        var tunnelListener = new TcpListener(IPAddress.Any, tunnelPort);
        tunnelListener.Start();

        Console.WriteLine($"Waiting for tunnel connection on port {tunnelPort}");

        using (var tunnelClient = await tunnelListener.AcceptTcpClientAsync())
        using (var tunnelStream = tunnelClient.GetStream())
        {
            Console.WriteLine("Tunnel client connected. Forwarding traffic...");

            Task clientToTunnel = ForwardDataAsync(sslStream, tunnelStream, "Client -> Tunnel");
            Task tunnelToClient = ForwardDataAsync(tunnelStream, sslStream, "Tunnel -> Client");

            await Task.WhenAny(clientToTunnel, tunnelToClient);
        }

        tunnelListener.Stop();
    }

    static async Task HandleHttpMode(SslStream sslStream)
    {
        try
        {
            using (var localClient = new TcpClient())
            {
                Console.WriteLine($"Attempting to connect to local service on port {LocalPort}...");
                await localClient.ConnectAsync(IPAddress.Loopback, LocalPort);
                using (var localStream = localClient.GetStream())
                {
                    Console.WriteLine($"Connected to local service on port {LocalPort}. Forwarding traffic...");

                    Task clientToLocal = ForwardDataAsync(sslStream, localStream, "Client -> Local", true);
                    Task localToClient = ForwardDataAsync(localStream, sslStream, "Local -> Client", false);

                    await Task.WhenAny(clientToLocal, localToClient);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to local service: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    static async Task ForwardDataAsync(Stream source, Stream destination, string direction, bool modifyHeaders = false)
    {
        byte[] buffer = new byte[4096];
        try
        {
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                if (modifyHeaders && direction == "Client -> Local")
                {
                    string data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    data = data.Replace($"Host: {ServerCertificate.GetNameInfo(X509NameType.DnsName, false)}:{ServerPort}", $"Host: localhost:{LocalPort}");
                    buffer = Encoding.ASCII.GetBytes(data);
                    bytesRead = buffer.Length;
                }
                await destination.WriteAsync(buffer, 0, bytesRead);
                Console.WriteLine($"{direction}: Forwarded {bytesRead} bytes");
            }
        }
        catch (IOException)
        {
            Console.WriteLine($"{direction}: Connection closed");
        }
    }

    static bool VerifyKey(byte[] key)
    {
        byte[] expectedKey = new byte[] { 0x00, 0x08, 0x00, 0x00, 0x00, 0x22, 0x4D, 0x00, 0x00, 0x00 };
        return key.AsSpan().SequenceEqual(expectedKey);
    }

    private static bool ValidateClientCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        if (UseHttp)
        {
            Console.WriteLine("HTTP mode: Accepting connection without client certificate.");
            return true;
        }

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
}