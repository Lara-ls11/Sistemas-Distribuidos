using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

class Program
{
    static void Main()
    {
        var server = new TcpListener(IPAddress.Any, 5002);
        server.Start();
        Console.WriteLine("Servidor à espera de dados..."); // ESTA LINHA É ESSENCIAL

        while (true)
        {
            using var client = server.AcceptTcpClient();
            var ns = client.GetStream();
            byte[] buf = new byte[1024];
            int n = ns.Read(buf, 0, buf.Length);
            string msg = Encoding.UTF8.GetString(buf, 0, n);
            Console.WriteLine("Servidor recebeu: " + msg);

            if (msg.StartsWith("STORE_DATA"))
                ns.Write(Encoding.UTF8.GetBytes("ACK_STORE"));
        }
    }
}
