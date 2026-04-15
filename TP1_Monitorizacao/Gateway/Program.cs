using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

class Program
{
    static void Main()
    {
        var gw = new TcpListener(IPAddress.Any, 5001);
        gw.Start();
        Console.WriteLine("Gateway à espera do sensor...");

        while (true)
        {
            var sensor = gw.AcceptTcpClient();
            Console.WriteLine("Sensor conectado: " + sensor.Client.RemoteEndPoint);
            using (sensor)
            {
                var nsS = sensor.GetStream();
                byte[] buf = new byte[1024];
                try
                {
                    while (true)
                    {
                        int n = nsS.Read(buf, 0, buf.Length);
                        if (n <= 0)
                        {
                            Console.WriteLine("Sensor desconectado.");
                            break;
                        }

                        string msg = Encoding.UTF8.GetString(buf, 0, n);
                        Console.WriteLine("Gateway recebeu: " + msg);

                        if (msg == "INIT") nsS.Write(Enc("ACK_INIT"));
                        else if (msg.StartsWith("CAPABILITIES")) nsS.Write(Enc("ACK_CAPABILITIES"));
                        else if (msg.StartsWith("DATA"))
                        {
                            nsS.Write(Enc("ACK_DATA"));
                            try
                            {
                                using var serv = new TcpClient("127.0.0.1", 5002);
                                var nsV = serv.GetStream();
                                nsV.Write(Enc("STORE_DATA:" + msg));
                                int m = nsV.Read(buf, 0, buf.Length);
                                Console.WriteLine("Servidor respondeu: " + Encoding.UTF8.GetString(buf, 0, m));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Erro ao contactar servidor: " + ex.Message);
                            }
                        }
                        else if (msg == "END")
                        {
                            nsS.Write(Enc("ACK_END"));
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Erro na ligação ao sensor: " + ex.Message);
                }
            }
        }
    }

    static byte[] Enc(string s) => Encoding.UTF8.GetBytes(s);
}
