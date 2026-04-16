using System;
using System.Net.Sockets;
using System.Text;
using System.Globalization;

class Program
{
    static void Main(string[] a)
    {
        try
        {
            var c = new TcpClient(a.Length > 0 ? a[0] : "127.0.0.1", 5001);
            var ns = c.GetStream();
            ns.ReadTimeout = 5000;

            Console.WriteLine("Conectado ao gateway.");

            // INIT
            Send(ns, "INIT");
            Rec(ns);

            // CAPABILITIES
            Send(ns, "CAPABILITIES:TEMP,HUM");
            Rec(ns);

            while (true)
            {
                Console.WriteLine("\n1 - Enviar TEMP e HUM");
                Console.WriteLine("2 - END");
                Console.Write("Opção: ");
                string op = Console.ReadLine();

                if (op == "1")
                {
                    // ============================
                    // TEMP
                    // ============================
                    Console.Write("Valor TEMP (apenas número): ");
                    string tempStr = Console.ReadLine().Trim();

                    if (!double.TryParse(tempStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double tempVal))
                    {
                        Console.WriteLine("TEMP inválido. Usa formato 20 ou 20.5");
                        continue;
                    }

                    string tempMsg = $"DATA:TEMP:{tempVal.ToString(CultureInfo.InvariantCulture)}";
                    Console.WriteLine("A enviar: " + tempMsg);
                    Send(ns, tempMsg);
                    Rec(ns);

                    // ============================
                    // HUM
                    // ============================
                    Console.Write("Valor HUM (apenas número): ");
                    string humStr = Console.ReadLine().Trim();

                    if (!double.TryParse(humStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double humVal))
                    {
                        Console.WriteLine("HUM inválido. Usa formato 40 ou 40.5");
                        continue;
                    }

                    string humMsg = $"DATA:HUM:{humVal.ToString(CultureInfo.InvariantCulture)}";
                    Console.WriteLine("A enviar: " + humMsg);
                    Send(ns, humMsg);
                    Rec(ns);

                    continue;
                }
                else if (op == "2")
                {
                    Send(ns, "END");
                    Rec(ns);
                    break;
                }
                else
                {
                    Console.WriteLine("Opção inválida.");
                    continue;
                }
            }

            c.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro: " + ex.Message);
        }
    }

    // ============================================================
    // ENVIO — ADICIONA \n PARA O GATEWAY CONSEGUIR LER A MENSAGEM
    // ============================================================
    static void Send(NetworkStream ns, string s)
    {
        s = s + "\n"; // ESSENCIAL
        byte[] data = Encoding.UTF8.GetBytes(s);
        ns.Write(data, 0, data.Length);
    }

    // ============================================================
    // RECEÇÃO
    // ============================================================
    static void Rec(NetworkStream ns)
    {
        try
        {
            byte[] b = new byte[1024];
            int n = ns.Read(b, 0, b.Length);

            if (n <= 0)
                Console.WriteLine("Recebido: <conexão fechada>");
            else
                Console.WriteLine("Recebido: " + Encoding.UTF8.GetString(b, 0, n));
        }
        catch (System.IO.IOException ie)
        {
            Console.WriteLine("Timeout / erro ao receber: " + ie.Message);
        }
    }
}
