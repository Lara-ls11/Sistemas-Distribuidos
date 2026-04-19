using System;
using System.Net.Sockets;
using System.Text;
using System.Globalization;
using System.Threading;

class Program
{
    static bool running = true;

    static void Main(string[] a)
    {
        try
        {
            string gatewayIp = a.Length > 0 ? a[0] : "127.0.0.1";

            var c = new TcpClient(gatewayIp, 5001);
            var ns = c.GetStream();
            ns.ReadTimeout = 5000;

            Console.WriteLine("Conectado ao gateway.");

            // INIT
            Send(ns, "INIT");
            Rec(ns);

            // ID DO SENSOR
            string sensorId = "SENSOR_001";
            Send(ns, $"ID:{sensorId}");
            Rec(ns);

            // CAPABILITIES
            Send(ns, "CAPABILITIES:TEMP,HUM");
            Rec(ns);

            // HEARTBEAT THREAD
            Thread heartbeatThread = new Thread(() =>
            {
                while (running)
                {
                    Send(ns, "HEARTBEAT");
                    Thread.Sleep(5000);
                }
            });

            heartbeatThread.IsBackground = true;
            heartbeatThread.Start();

            // MENU PRINCIPAL
            while (true)
            {
                Console.WriteLine("\n1 - Enviar TEMP e HUM");
                Console.WriteLine("2 - END");
                Console.Write("Opção: ");
                string op = Console.ReadLine();

                if (op == "1")
                {
                    // TEMP
                    Console.Write("Valor TEMP (apenas número): ");
                    string tempStr = Console.ReadLine().Trim().Replace(',', '.');

                    if (!double.TryParse(tempStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double tempVal))
                    {
                        Console.WriteLine("TEMP inválido. Usa formato 20 ou 20.5");
                        continue;
                    }

                    string tempMsg = $"DATA:TEMP:{tempVal.ToString(CultureInfo.InvariantCulture)}";
                    Console.WriteLine("A enviar: " + tempMsg);
                    Send(ns, tempMsg);
                    Rec(ns);

                    // HUM
                    Console.Write("Valor HUM (apenas número): ");
                    string humStr = Console.ReadLine().Trim().Replace(',', '.');

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
                    running = false;
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

    // ENVIO
    static void Send(NetworkStream ns, string s)
    {
        s = s + "\n";
        byte[] data = Encoding.UTF8.GetBytes(s);
        ns.Write(data, 0, data.Length);
    }

    // RECEÇÃO
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
