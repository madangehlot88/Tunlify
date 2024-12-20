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
            //TunnelPort = GetAvailablePort();
            TunnelPort = 5900;

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
            // The client has disconnected
            Console.WriteLine($"{direction}: Connection closed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in {direction}: {ex.Message}");
        }
    }
    private static bool ValidateClientCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        // Convert the certificate to X509Certificate2
        X509Certificate2 cert2 = new X509Certificate2(certificate);

        //// Log certificate details
        //Console.WriteLine($"Client Certificate Details:");
        //Console.WriteLine($"Subject: {cert2.Subject}");
        //Console.WriteLine($"Issuer: {cert2.Issuer}");
        //Console.WriteLine($"Thumbprint: {cert2.Thumbprint}");
        //Console.WriteLine($"Not Before: {cert2.NotBefore}");
        //Console.WriteLine($"Not After: {cert2.NotAfter}");

        //// For testing purposes, accept all client certificates
        //// WARNING: This is not secure for production use!
        //Console.WriteLine("Accepting all client certificates for testing purposes.");
        //return true;

        // Define the allowed thumbprint(s)
        string[] allowedThumbprints = { "B009F424AB0683DD7BF68BA98193132B5A298814" };

        // Check if the certificate's thumbprint is in the list of allowed thumbprints
        if (!allowedThumbprints.Contains(cert2.Thumbprint))
        {
            Console.WriteLine($"Client certificate is not in the list of allowed certificates. Thumbprint: {cert2.Thumbprint}");
            return false;
        }

        // Ignore chain validation errors for self-signed certificates
        if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors)
        {
            Console.WriteLine("Ignoring chain validation errors for self-signed certificate.");
            return true;
        }

        // If we get here, the certificate is valid
        Console.WriteLine("Client certificate validated successfully.");
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