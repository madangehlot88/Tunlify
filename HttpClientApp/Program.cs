using System;
using System.Net.Sockets;
using System.Threading.Tasks;

class TunnelClient
{
    static async Task Main(string[] args)
    {
        string serverAddress = "167.71.227.50";
        int tunnelPort = 8001;
        int localPort = 5000; // Your local web server port

        using (TcpClient tunnelClient = new TcpClient())
        {
            await tunnelClient.ConnectAsync(serverAddress, tunnelPort);
            Console.WriteLine("Connected to tunnel server");

            using (TcpClient localClient = new TcpClient("localhost", localPort))
            using (NetworkStream tunnelStream = tunnelClient.GetStream())
            using (NetworkStream localStream = localClient.GetStream())
            {
                Task task1 = tunnelStream.CopyToAsync(localStream);
                Task task2 = localStream.CopyToAsync(tunnelStream);
                await Task.WhenAny(task1, task2);
            }
        }
    }
}