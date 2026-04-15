using System;
using System.Net.Sockets;
using System.Text;

class Program
{
    static void Main(string[] a)
    {
        try
        {
            var c = new TcpClient(a.Length > 0 ? a[0] : "127.0.0.1", 5001);
            var ns = c.GetStream();
            // set a read timeout so the client doesn't block forever
            ns.ReadTimeout = 5000;

            Console.WriteLine("Conectado ao gateway.");

            Console.WriteLine("A enviar: INIT");
            Send(ns, "INIT");
            Console.WriteLine("A aguardar ACK_INIT...");
            Rec(ns);

            Console.WriteLine("A enviar: CAPABILITIES:TEMP,HUM");
            Send(ns, "CAPABILITIES:TEMP,HUM");
            Console.WriteLine("A aguardar ACK_CAPABILITIES...");
            Rec(ns);

            while (true)
            {
                Console.WriteLine("1-DATA  2-END");
                string op = Console.ReadLine();

                if (op == "1")
                {
                    Console.Write("Valor: ");
                    string val = Console.ReadLine();
                    Console.WriteLine("A enviar: DATA:TEMP:{0}", val);
                    Send(ns, "DATA:TEMP:" + val);
                    Console.WriteLine("A aguardar ACK_DATA / resposta do servidor...");
                    Rec(ns);
                }
                else
                {
                    Console.WriteLine("A enviar: END");
                    Send(ns, "END");
                    Console.WriteLine("A aguardar ACK_END...");
                    Rec(ns);
                    break;
                }
            }

            c.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro: " + ex.Message);
        }
    }

    static void Send(NetworkStream ns, string s) =>
        ns.Write(Encoding.UTF8.GetBytes(s));

    static void Rec(NetworkStream ns)
    {
        try
        {
            byte[] b = new byte[1024];
            int n = ns.Read(b, 0, b.Length);
            if (n <= 0) Console.WriteLine("Recebido: <conexão fechada>");
            else Console.WriteLine("Recebido: " + Encoding.UTF8.GetString(b, 0, n));
        }
        catch (System.IO.IOException ie)
        {
            Console.WriteLine("Timeout / erro ao receber: " + ie.Message);
        }
    }
}
