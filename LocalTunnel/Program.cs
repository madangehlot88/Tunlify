using System.Diagnostics;
using System.Net.Sockets;
using System.Net;

 TcpListener listener = new TcpListener(IPAddress.Any, 3742);

const int BUFFER_SIZE = 4096;

listener.Start();
new Task(() => {
    // Accept clients.
    while (true)
    {
        var client = listener.AcceptTcpClient();
        new Task(() => {
            // Handle this client.
            var clientStream = client.GetStream();
            TcpClient server = new TcpClient("localhost", 5900);
            var serverStream = server.GetStream();
            new Task(() => {
                byte[] message = new byte[BUFFER_SIZE];
                int clientBytes;
                while (true)
                {
                    try
                    {
                        clientBytes = clientStream.Read(message, 0, BUFFER_SIZE);
                    }
                    catch
                    {
                        // Socket error - exit loop.  Client will have to reconnect.
                        break;
                    }
                    if (clientBytes == 0)
                    {
                        // Client disconnected.
                        break;
                    }
                    serverStream.Write(message, 0, clientBytes);
                }
                client.Close();
            }).Start();
            new Task(() => {
                byte[] message = new byte[BUFFER_SIZE];
                int serverBytes;
                while (true)
                {
                    try
                    {
                        serverBytes = serverStream.Read(message, 0, BUFFER_SIZE);
                        clientStream.Write(message, 0, serverBytes);
                    }
                    catch
                    {
                        // Server socket error - exit loop.  Client will have to reconnect.
                        break;
                    }
                    if (serverBytes == 0)
                    {
                        // server disconnected.
                        break;
                    }
                }
            }).Start();
        }).Start();
    }
}).Start();
Console.WriteLine("Server listening on port 3742.  Press enter to exit.");
Console.ReadLine();
listener.Stop();