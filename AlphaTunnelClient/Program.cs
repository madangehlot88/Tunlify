using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

class ImprovedTcpTunnel
{
    private const string ServerIp = "167.71.227.50";
    private const int ServerPort = 5900;
    private const string RdpIp = "10.0.0.102";
    private const int RdpPort = 3389;

    private static readonly X509Certificate2 ClientCertificate = new X509Certificate2("client.pfx", "password");

    static async Task Main(string[] args)
    {
        while (true)
        {
            try
            {
                await ConnectAndForwardAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(5)); // Wait before retrying
            }
        }
    }

    static async Task ConnectAndForwardAsync()
    {
        using (TcpClient serverClient = new TcpClient())
        using (TcpClient rdpClient = new TcpClient())
        {
            Console.WriteLine($"Connecting to server at {ServerIp}:{ServerPort}...");
            await serverClient.ConnectAsync(ServerIp, ServerPort);

            using (SslStream sslStream = new SslStream(serverClient.GetStream(), false, ValidateServerCertificate, null))
            {
                await sslStream.AuthenticateAsClientAsync(ServerIp, new X509CertificateCollection { ClientCertificate }, System.Security.Authentication.SslProtocols.Tls12, false);

                // Send the key
                byte[] key = new byte[] { 0, 8, 0, 0, 0, 34, 77, 0, 0, 0 };
                await sslStream.WriteAsync(key, 0, key.Length);

                // Receive the IP address (16 bytes)
                byte[] ipBytes = new byte[16];
                await sslStream.ReadAsync(ipBytes, 0, ipBytes.Length);

                Console.WriteLine("Connected to server and key verified.");

                Console.WriteLine($"Connecting to RDP at {RdpIp}:{RdpPort}...");
                await rdpClient.ConnectAsync(RdpIp, RdpPort);
                Console.WriteLine("Connected to RDP server.");

                using (NetworkStream rdpStream = rdpClient.GetStream())
                {
                    Console.WriteLine("Forwarding traffic...");
                    using (var cts = new CancellationTokenSource())
                    {
                        Task serverToRdp = ForwardTrafficAsync(sslStream, rdpStream, "Server -> RDP", cts.Token);
                        Task rdpToServer = ForwardTrafficAsync(rdpStream, sslStream, "RDP -> Server", cts.Token);

                        await Task.WhenAny(serverToRdp, rdpToServer);
                        cts.Cancel(); // Cancel the other task when one completes
                        await Task.WhenAll(serverToRdp, rdpToServer); // Wait for both tasks to complete
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

    // For the client to validate the server's certificate
    public static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        Console.WriteLine($"Certificate error: {sslPolicyErrors}");

        // Certificate validation error handling
        if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) != 0)
        {
            foreach (X509ChainStatus status in chain.ChainStatus)
            {
                if (status.Status != X509ChainStatusFlags.RevocationStatusUnknown)
                {
                    chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                    chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;

                    if (!chain.Build((X509Certificate2)certificate))
                    {
                        Console.WriteLine($"Chain building failed: {status.StatusInformation}");
                        return false;
                    }
                }
            }
        }

        // Implement your specific validation logic here
        // For example, you might want to check if the certificate is in a list of trusted certificates
        // or if it's issued by a trusted Certificate Authority

        // Example: Check if the certificate is issued to the expected server
        string expectedServerName = "your_server_name";
        if (!certificate.Subject.Contains($"CN={expectedServerName}"))
        {
            Console.WriteLine("Certificate is not issued to the expected server.");
            return false;
        }

        // Example: Check certificate expiration
        if (DateTime.Parse(certificate.GetExpirationDateString()) < DateTime.Now)
        {
            Console.WriteLine("Certificate has expired.");
            return false;
        }

        // If we get here, we're satisfied with the certificate
        return true;
    }
}