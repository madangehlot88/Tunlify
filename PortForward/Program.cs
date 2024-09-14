using System.Net.Sockets;
using System.Net;

public class AsynchronousSocketListener
{
    private static Socket _forwardSocket;
    private static Socket _rdpSocket;
    private static readonly byte[] _forwardBuffer = new byte[8192];
    private static readonly byte[] _rdpBuffer = new byte[8192];

    public static void Main()
    {
        var ipHostInfo = Dns.GetHostEntry("localhost");
        var ipAddress = ipHostInfo.AddressList[0];
        Console.WriteLine(ipAddress);
        var localEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000);

        // Create a TCP/IP socket.  
        var listener = new Socket(ipAddress.AddressFamily,
        SocketType.Stream, ProtocolType.Tcp);

        listener.Bind(localEndPoint);
        listener.Listen(100);

        listener.BeginAccept(AcceptCallback, listener);

        Console.WriteLine("\nPress ENTER to continue...");
        Console.Read();

    }

    public static void AcceptCallback(IAsyncResult ar)
    {
        // Get the socket that handles the client request.  
        var listener = (Socket)ar.AsyncState;
        _forwardSocket = listener.EndAccept(ar);

        _forwardSocket.BeginReceive(_forwardBuffer, 0, _forwardBuffer.Length, SocketFlags.None, ReadCallback, null);
    }

    public static void ReadCallback(IAsyncResult ar)
    {
        Console.WriteLine("ReadCallback");
        // Read data from the client socket.
        var bytesRead = _forwardSocket.EndReceive(ar);
        Console.WriteLine($"ReadCallback bytes read: {bytesRead}");

        if (bytesRead > 0)
        {
            SendToRdpServer(bytesRead);
        }
        _forwardSocket.BeginReceive(_forwardBuffer, 0, _forwardBuffer.Length, SocketFlags.None, ReadCallback, null);
    }


    private static void SendToRdpServer(int bytesRead)
    {
        Console.WriteLine("SendToRdpServer");
        if (_rdpSocket == null)
        {
            Console.WriteLine("**** Establishing the connection to the RDP server");
            var ipHostInfo = Dns.GetHostEntry("152.59.100.3");
            var ipAddress = ipHostInfo.AddressList[0];
            var endPoint = new IPEndPoint(ipAddress, 3389);

            // Create a TCP/IP socket.  
            _rdpSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _rdpSocket.Connect(endPoint);
        }
        Console.WriteLine("Sending the data to the RDP server");
        _rdpSocket.Send(_forwardBuffer, 0, bytesRead, SocketFlags.None);
        _rdpSocket.BeginReceive(_rdpBuffer, 0, _rdpBuffer.Length, SocketFlags.None, ReadCallbackFromRdpServer, null);

    }

    public static void ReadCallbackFromRdpServer(IAsyncResult ar)
    {
        Console.WriteLine("ReadCallbackFromRdpServer");
        // Read data from the client socket.
        var bytesRead = _rdpSocket.EndReceive(ar);
        Console.WriteLine($"ReadCallbackFromRdpServer bytes read: {bytesRead}");

        if (bytesRead > 0)
        {
            Console.WriteLine("Relaying the data back");
            _forwardSocket.Send((_rdpBuffer), 0, bytesRead, SocketFlags.None);
        }
        _rdpSocket.BeginReceive(_rdpBuffer, 0, _rdpBuffer.Length, SocketFlags.None, ReadCallbackFromRdpServer, null);
    }
}