using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

class TcpTunnelClient
{
    private static string ServerAddress;
    private static int ServerPort;
    private static int LocalPort = 5000; // Your local web server port
    private static X509Certificate2 ClientCertificate;

    static async Task Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run <ServerAddress> <ServerPort> [--local-port <Port>] [--cert <path> <password>]");
            return;
        }

        ServerAddress = args[0];
        if (!int.TryParse(args[1], out ServerPort))
        {
            Console.WriteLine("Invalid server port");
            return;
        }

        for (int i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--local-port":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int port))
                    {
                        LocalPort = port;
                        Console.WriteLine($"Local port set to: {LocalPort}");
                    }
                    break;
                case "--cert":
                    if (i + 2 < args.Length)
                    {
                        string certPath = args[++i];
                        string certPassword = args[++i];
                        ClientCertificate = new X509Certificate2(certPath, certPassword);
                        Console.WriteLine($"Client certificate loaded: {ClientCertificate.Subject}");
                    }
                    break;
            }
        }

        try
        {
            await ConnectToServer();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static async Task ConnectToServer()
    {
        using (TcpClient client = new TcpClient())
        {
            await client.ConnectAsync(ServerAddress, ServerPort);
            Console.WriteLine($"Connected to server: {ServerAddress}:{ServerPort}");

            using (SslStream sslStream = new SslStream(client.GetStream(), false, ValidateServerCertificate, null))
            {
                try
                {
                    Console.WriteLine("Starting SSL/TLS handshake...");
                    var sslClientAuthOptions = new SslClientAuthenticationOptions
                    {
                        TargetHost = ServerAddress,
                        ClientCertificates = new X509CertificateCollection { ClientCertificate },
                        EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                    };

                    await sslStream.AuthenticateAsClientAsync(sslClientAuthOptions);

                    Console.WriteLine("SSL/TLS handshake completed");
                    Console.WriteLine($"SSL/TLS version: {sslStream.SslProtocol}");

                    await HandleHttpMode(sslStream);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SSL/TLS error: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }
        }
    }

    static async Task HandleHttpMode(SslStream sslStream)
    {
        try
        {
            using (TcpClient localClient = new TcpClient("localhost", LocalPort))
            using (NetworkStream localStream = localClient.GetStream())
            {
                Console.WriteLine($"Connected to local web server on port {LocalPort}");

                Task serverToLocal = ForwardDataAsync(sslStream, localStream, "Server -> Local");
                Task localToServer = ForwardDataAsync(localStream, sslStream, "Local -> Server");

                await Task.WhenAny(serverToLocal, localToServer);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in HTTP mode: {ex.Message}");
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

    private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        Console.WriteLine($"Certificate error: {sslPolicyErrors}");

        // For testing purposes, accept any certificate
        return true;
    }
}