using System;
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
    private static int ServerPort = 3742; // The port you want to expose on the remote server
    private static bool AllowAllCertificates = false;
    private static List<string> AllowedThumbprints = new List<string>();
    private static readonly X509Certificate2 ServerCertificate = new X509Certificate2("server.pfx", "1234");

    static async Task Main(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
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
                Console.WriteLine($"New connection from: {((IPEndPoint)client.Client.RemoteEndPoint).Address}");

                using (SslStream sslStream = new SslStream(client.GetStream(), false, ValidateClientCertificate))
                {
                    try
                    {
                        var sslServerAuthOptions = new SslServerAuthenticationOptions
                        {
                            ServerCertificate = ServerCertificate,
                            ClientCertificateRequired = false,
                            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                            RemoteCertificateValidationCallback = ValidateClientCertificate
                        };

                        await sslStream.AuthenticateAsServerAsync(sslServerAuthOptions);
                        Console.WriteLine($"SSL/TLS connection established. Protocol: {sslStream.SslProtocol}");
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("Non-SSL connection detected. Proceeding with plain connection.");
                        await HandleHttpMode(client.GetStream());
                        return;
                    }

                    await HandleHttpMode(sslStream);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }

    static async Task HandleHttpMode(Stream stream)
    {
        TcpListener httpListener = new TcpListener(IPAddress.Any, ServerPort);
        httpListener.Start();

        try
        {
            Console.WriteLine($"HTTP listener started on port {ServerPort}");

            while (true)
            {
                TcpClient httpClient = await httpListener.AcceptTcpClientAsync();
                _ = ProcessHttpRequestAsync(httpClient, stream);
            }
        }
        finally
        {
            httpListener.Stop();
        }
    }

    static async Task ProcessHttpRequestAsync(TcpClient httpClient, Stream tunnelStream)
    {
        using (httpClient)
        using (NetworkStream httpStream = httpClient.GetStream())
        {
            Console.WriteLine("HTTP request received. Forwarding to tunnel...");

            Task httpToTunnel = ForwardDataAsync(httpStream, tunnelStream, "HTTP -> Tunnel");
            Task tunnelToHttp = ForwardDataAsync(tunnelStream, httpStream, "Tunnel -> HTTP");

            await Task.WhenAny(httpToTunnel, tunnelToHttp);
        }
    }

    static async Task ForwardDataAsync(Stream source, Stream destination, string direction)
    {
        byte[] buffer = new byte[4096];
        try
        {
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead);
                Console.WriteLine($"{direction}: Forwarded {bytesRead} bytes");
            }
        }
        catch (IOException)
        {
            Console.WriteLine($"{direction}: Connection closed");
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
            return true; // Allow connections without client certificates
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