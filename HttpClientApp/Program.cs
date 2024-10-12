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
    private static int LocalPort = 8080;
    private static bool UseHttp = false;
    private static X509Certificate2 ClientCertificate;

    static async Task Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run <ServerAddress> <ServerPort> [--http] [--local-port <Port>] [--cert <path> <password>]");
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
        using (TcpClient client = new TcpClient(ServerAddress, ServerPort))
        {
            Console.WriteLine($"Connected to server: {ServerAddress}:{ServerPort}");

            using (SslStream sslStream = new SslStream(client.GetStream(), false, ValidateServerCertificate, null))
            {
                try
                {
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

                    if (!UseHttp)
                    {
                        await HandleOriginalProtocol(sslStream);
                    }
                    else
                    {
                        await HandleHttpMode(sslStream);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SSL/TLS error: {ex.Message}");
                }
            }
        }
    }

    static async Task HandleOriginalProtocol(SslStream sslStream)
    {
        byte[] key = new byte[] { 0x00, 0x08, 0x00, 0x00, 0x00, 0x22, 0x4D, 0x00, 0x00, 0x00 };
        await sslStream.WriteAsync(key, 0, key.Length);

        byte[] response = new byte[20];
        await sslStream.ReadAsync(response, 0, response.Length);

        int tunnelPort = BitConverter.ToInt32(response, 16);
        Console.WriteLine($"Received tunnel port: {tunnelPort}");

        using (TcpClient tunnelClient = new TcpClient(ServerAddress, tunnelPort))
        using (NetworkStream tunnelStream = tunnelClient.GetStream())
        {
            Console.WriteLine($"Connected to tunnel port: {tunnelPort}");

            using (TcpListener localListener = new TcpListener(System.Net.IPAddress.Any, LocalPort))
            {
                localListener.Start();
                Console.WriteLine($"Listening on local port: {LocalPort}");

                while (true)
                {
                    using (TcpClient localClient = await localListener.AcceptTcpClientAsync())
                    using (NetworkStream localStream = localClient.GetStream())
                    {
                        Console.WriteLine("Local connection accepted. Forwarding data...");

                        Task clientToTunnel = ForwardDataAsync(localStream, tunnelStream, "Local -> Tunnel");
                        Task tunnelToClient = ForwardDataAsync(tunnelStream, localStream, "Tunnel -> Local");

                        await Task.WhenAny(clientToTunnel, tunnelToClient);
                    }
                }
            }
        }
    }

    static async Task HandleHttpMode(SslStream sslStream)
    {
        TcpListener localListener = null;
        try
        {
            localListener = new TcpListener(System.Net.IPAddress.Any, LocalPort);
            localListener.Start();
            Console.WriteLine($"HTTP mode: Listening on local port: {LocalPort}");

            while (true)
            {
                using (TcpClient localClient = await localListener.AcceptTcpClientAsync())
                using (NetworkStream localStream = localClient.GetStream())
                {
                    Console.WriteLine("Local HTTP connection accepted. Forwarding data...");

                    Task clientToServer = ForwardDataAsync(localStream, sslStream, "Local -> Server");
                    Task serverToClient = ForwardDataAsync(sslStream, localStream, "Server -> Local");

                    await Task.WhenAny(clientToServer, serverToClient);
                }
            }
        }
        catch (SocketException se)
        {
            if (se.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                Console.WriteLine($"Error: Port {LocalPort} is already in use. Please choose a different port.");
            }
            else
            {
                Console.WriteLine($"SocketException: {se.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in HTTP mode: {ex.Message}");
        }
        finally
        {
            localListener?.Stop();
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

        // You may want to add additional certificate validation logic here
        // For now, we'll accept the certificate despite errors (not recommended for production)
        return true;
    }
}