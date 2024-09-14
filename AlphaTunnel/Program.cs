using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

class ImprovedTcpTunnelServer
{
    private static int ServerPort;
    private static string LocalIp;
    private static int TunnelPort;

    private static readonly X509Certificate2 ServerCertificate = new X509Certificate2("server.pfx", "1234");

    static async Task Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("Usage: TcpTunnelServer <TunnelPort>");
            return;
        }

        if (!int.TryParse(args[0], out TunnelPort))
        {
            Console.WriteLine("Invalid TunnelPort. Please provide a valid port number.");
            return;
        }

        try
        {
            ServerPort = GetAvailablePort();
            LocalIp = GetLocalIpAddress();

            var listener = new TcpListener(IPAddress.Any, ServerPort);
            listener.Start();
            Console.WriteLine($"Server listening on port {ServerPort}");
            Console.WriteLine($"Tunnel endpoint: {LocalIp}:{TunnelPort}");

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client);
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            // Console.WriteLine($"Error: Address already in use. {ex.Message}");
            // We're commenting out this log as requested
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

    static string GetLocalIpAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }

    static async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                using (SslStream sslStream = new SslStream(client.GetStream(), false, ValidateClientCertificate, SelectServerCertificate))
                {
                    await sslStream.AuthenticateAsServerAsync(ServerCertificate, clientCertificateRequired: true, SslProtocols.Tls12, checkCertificateRevocation: true);

                    // Read the key
                    byte[] keyBuffer = new byte[10];
                    await sslStream.ReadAsync(keyBuffer, 0, keyBuffer.Length);

                    if (!VerifyKey(keyBuffer))
                    {
                        Console.WriteLine("Invalid key received. Closing connection.");
                        return;
                    }

                    // Send back the client's IP address and the server port
                    byte[] ipBytes = ((IPEndPoint)client.Client.RemoteEndPoint).Address.GetAddressBytes();
                    byte[] portBytes = BitConverter.GetBytes(ServerPort);
                    byte[] response = new byte[20]; // 16 bytes for IP, 4 bytes for port
                    Array.Copy(ipBytes, 0, response, 0, ipBytes.Length);
                    Array.Copy(portBytes, 0, response, 16, 4);
                    await sslStream.WriteAsync(response, 0, response.Length);

                    Console.WriteLine($"Client connected: {((IPEndPoint)client.Client.RemoteEndPoint).Address}");

                    using (TcpClient tunnelClient = new TcpClient())
                    {
                        await tunnelClient.ConnectAsync(LocalIp, TunnelPort);
                        using (NetworkStream tunnelStream = tunnelClient.GetStream())
                        {
                            Console.WriteLine($"Connected to tunnel endpoint at {LocalIp}:{TunnelPort}. Forwarding traffic...");
                            using (var cts = new CancellationTokenSource())
                            {
                                Task clientToTunnel = ForwardTrafficAsync(sslStream, tunnelStream, "Client -> Tunnel", cts.Token);
                                Task tunnelToClient = ForwardTrafficAsync(tunnelStream, sslStream, "Tunnel -> Client", cts.Token);

                                await Task.WhenAny(clientToTunnel, tunnelToClient);
                                cts.Cancel(); // Cancel the other task when one completes
                                await Task.WhenAll(clientToTunnel, tunnelToClient); // Wait for both tasks to complete
                            }
                        }
                    }
                }
            }
            catch (AuthenticationException ex)
            {
                Console.WriteLine($"SSL/TLS authentication failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
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

    private static bool ValidateClientCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        Console.WriteLine($"Certificate error: {sslPolicyErrors}");

        // Implement your specific validation logic here
        // This might include checking against a list of known client certificates,
        // verifying the certificate's thumbprint, or other custom logic

        // Example: Check if the certificate is in a list of allowed thumbprints
        string[] allowedThumbprints = { "AABBCCDDEEFF00112233445566778899AABBCCDD"}; // Replace with actual thumbprints
        X509Certificate2 cert2 = new X509Certificate2(certificate);
        if (!Array.Exists(allowedThumbprints, thumbprint => thumbprint == cert2.Thumbprint))
        {
            Console.WriteLine("Client certificate is not in the list of allowed certificates.");
            return false;
        }

        // Check certificate expiration
        if (DateTime.Parse(certificate.GetExpirationDateString()) < DateTime.Now)
        {
            Console.WriteLine("Client certificate has expired.");
            return false;
        }

        // If we get here, we're satisfied with the certificate
        return true;
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