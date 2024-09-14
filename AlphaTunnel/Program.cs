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
    private const int ServerPort = 5900;
    private const string RdpIp = "10.0.0.102";
    private const int RdpPort = 3389;

    private static readonly X509Certificate2 ServerCertificate = new X509Certificate2("server.pfx", "password");

    static async Task Main(string[] args)
    {
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
                using (SslStream sslStream = new SslStream(client.GetStream(), false, ValidateClientCertificate, null))
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

                    // Send back the client's IP address
                    byte[] ipBytes = ((IPEndPoint)client.Client.RemoteEndPoint).Address.GetAddressBytes();
                    if (ipBytes.Length == 4)
                    {
                        byte[] paddedIpBytes = new byte[16];
                        Array.Copy(ipBytes, 0, paddedIpBytes, 12, 4);
                        ipBytes = paddedIpBytes;
                    }
                    await sslStream.WriteAsync(ipBytes, 0, ipBytes.Length);

                    Console.WriteLine($"Client connected: {((IPEndPoint)client.Client.RemoteEndPoint).Address}");

                    using (TcpClient rdpClient = new TcpClient())
                    {
                        await rdpClient.ConnectAsync(RdpIp, RdpPort);
                        using (NetworkStream rdpStream = rdpClient.GetStream())
                        {
                            Console.WriteLine("Connected to RDP server. Forwarding traffic...");
                            using (var cts = new CancellationTokenSource())
                            {
                                Task clientToRdp = ForwardTrafficAsync(sslStream, rdpStream, "Client -> RDP", cts.Token);
                                Task rdpToClient = ForwardTrafficAsync(rdpStream, sslStream, "RDP -> Client", cts.Token);

                                await Task.WhenAny(clientToRdp, rdpToClient);
                                cts.Cancel(); // Cancel the other task when one completes
                                await Task.WhenAll(clientToRdp, rdpToClient); // Wait for both tasks to complete
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

    // For the server to validate the client's certificate
    public static bool ValidateClientCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        Console.WriteLine($"Certificate error: {sslPolicyErrors}");

        // Implement your specific validation logic here
        // This might include checking against a list of known client certificates,
        // verifying the certificate's thumbprint, or other custom logic

        // Example: Check if the certificate is in a list of allowed thumbprints
        string[] allowedThumbprints = { "thumbprint1", "thumbprint2" };
        X509Certificate2 cert2 = new X509Certificate2(certificate);
        if (!Array.Exists(allowedThumbprints, thumbprint => thumbprint == cert2.Thumbprint))
        {
            Console.WriteLine("Client certificate is not in the list of allowed certificates.");
            return false;
        }

        // Example: Check certificate expiration
        if (DateTime.Parse(certificate.GetExpirationDateString()) < DateTime.Now)
        {
            Console.WriteLine("Client certificate has expired.");
            return false;
        }

        // If we get here, we're satisfied with the certificate
        return true;
    }

    private static bool VerifyKey(byte[] key)
    {
        byte[] expectedKey = new byte[] { 0, 8, 0, 0, 0, 34, 77, 0, 0, 0 };
        return key.AsSpan().SequenceEqual(expectedKey);
    }
}