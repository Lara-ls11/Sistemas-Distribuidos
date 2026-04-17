using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Program
{
    private static Mutex _fileMutex = new Mutex(); // Mutex para o ficheiro do servidor
    private static readonly string _dataFile = "server_data.log";

    static void Main()
    {
        var server = new TcpListener(IPAddress.Any, 5002);
        server.Start();
        Console.WriteLine("Servidor à espera de dados..."); 

        while (true)
        {
            var client = server.AcceptTcpClient();
            Thread clientThread = new Thread(() => HandleClient(client)) { IsBackground = true };
            clientThread.Start();
        }
    }

    static void HandleClient(TcpClient client)
    {
        try
        {
            using (client)
            {
                var ns = client.GetStream();
                byte[] buf = new byte[1024];
                int n = ns.Read(buf, 0, buf.Length);
                if (n == 0) return;

                string msg = Encoding.UTF8.GetString(buf, 0, n);
                Console.WriteLine("Servidor recebeu: " + msg);

                // Guarda informacao no ficheiro com exclusão mutua usando Mutex
                _fileMutex.WaitOne();
                try
                {
                    File.AppendAllText(_dataFile, msg + Environment.NewLine);
                }
                finally
                {
                    _fileMutex.ReleaseMutex();
                }

                if (msg.StartsWith("STORE_DATA") || msg.StartsWith("RAW_DATA") || msg.StartsWith("AGG_DATA"))
                {
                    byte[] response = Encoding.UTF8.GetBytes("ACK");
                    ns.Write(response, 0, response.Length);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro no atendimento: " + ex.Message);
        }
    }
}
