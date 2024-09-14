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
    private static int TunnelPort;  // This is the port the TcpListener will use
    private static int ServerPort;  // This is the port for the local service we're tunneling to

    private static readonly X509Certificate2 ServerCertificate = new X509Certificate2("server.pfx", "1234");

    static async Task Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("Usage: TcpTunnelServer <ServerPort>");
            return;
        }

        if (!int.TryParse(args[0], out ServerPort))
        {
            Console.WriteLine("Invalid ServerPort. Please provide a valid port number.");
            return;
        }

        try
        {
            TunnelPort = GetAvailablePort();

            var listener = new TcpListener(IPAddress.Any, TunnelPort);
            listener.Start();
            Console.WriteLine($"Tunnel listening on port {TunnelPort}");
            Console.WriteLine($"Forwarding to local service on port {ServerPort}");

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client);
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            // Address already in use error is silently handled
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

                    // Send back the client's IP address and the tunnel port
                    byte[] ipBytes = ((IPEndPoint)client.Client.RemoteEndPoint).Address.GetAddressBytes();
                    byte[] portBytes = BitConverter.GetBytes(TunnelPort);
                    byte[] response = new byte[20]; // 16 bytes for IP, 4 bytes for port
                    Array.Copy(ipBytes, 0, response, 0, ipBytes.Length);
                    Array.Copy(portBytes, 0, response, 16, 4);
                    await sslStream.WriteAsync(response, 0, response.Length);

                    Console.WriteLine($"Client connected: {((IPEndPoint)client.Client.RemoteEndPoint).Address}");

                    using (TcpClient serverClient = new TcpClient())
                    {
                        await serverClient.ConnectAsync(IPAddress.Loopback, ServerPort);
                        using (NetworkStream serverStream = serverClient.GetStream())
                        {
                            Console.WriteLine($"Connected to local service on port {ServerPort}. Forwarding traffic...");
                            using (var cts = new CancellationTokenSource())
                            {
                                Task clientToServer = ForwardTrafficAsync(sslStream, serverStream, "Client -> Server", cts.Token);
                                Task serverToClient = ForwardTrafficAsync(serverStream, sslStream, "Server -> Client", cts.Token);

                                await Task.WhenAny(clientToServer, serverToClient);
                                cts.Cancel(); // Cancel the other task when one completes
                                await Task.WhenAll(clientToServer, serverToClient); // Wait for both tasks to complete
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