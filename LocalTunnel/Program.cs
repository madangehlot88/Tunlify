﻿using System.Net.Sockets;
using System.Net;
using System.Text;

var ipEndPoint = new IPEndPoint(IPAddress.Any, 3742);
TcpListener listener = new(ipEndPoint);

try
{
    listener.Start();
    while (true)
    {
        Socket client = listener.AcceptSocket();
        Console.WriteLine("Connection accepted.");

        var childSocketThread = new Thread(() =>
        {
            byte[] data = new byte[100];
            int size = client.Receive(data);
            Console.WriteLine("Recieved data: ");

            for (int i = 0; i < size; i++)
            {
                Console.Write(Convert.ToChar(data[i]));
            }

            Console.WriteLine();

            client.Close();
        });

        childSocketThread.Start();
    }
}
finally
{
    listener.Stop();
}