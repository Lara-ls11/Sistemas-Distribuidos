using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gateway.Managers;
using Gateway.Services;

class Program
{
    private static SensorManager _sensorManager = new SensorManager();
    private static FileManager _fileManager = new FileManager();
    private static DataValidator _validator = new DataValidator();
    private static DataPreprocessor _preprocessor = new DataPreprocessor();
    private static int _sensorCounter = 0;
    private static readonly object _counterLock = new object();

    static void Main()
    {
        var gw = new TcpListener(IPAddress.Any, 5001);
        gw.Start();
        Console.WriteLine("[INFO] Gateway iniciado na porta 5001");
        Console.WriteLine("[INFO] À espera de sensores...");

        // Thread de limpeza periódica
        Task.Run(() => CleanupThread());

        while (true)
        {
            try
            {
                var sensor = gw.AcceptTcpClient();
                var remoteEndPoint = sensor.Client.RemoteEndPoint.ToString();
                
                // Gerar ID único para o sensor
                string sensorId;
                lock (_counterLock)
                {
                    _sensorCounter++;
                    sensorId = $"SENSOR_{_sensorCounter:000}";
                }

                Console.WriteLine($"[INFO] Sensor conectado: {remoteEndPoint} (ID: {sensorId})");
                
                // Processar sensor em thread separada
                Task.Run(() => HandleSensor(sensor, sensorId, remoteEndPoint));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Erro ao aceitar conexão: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Processa comunicação com um sensor em thread separada
    /// </summary>
    static void HandleSensor(TcpClient sensorClient, string sensorId, string remoteEndPoint)
    {
        try
        {
            using (sensorClient)
            {
                var nsS = sensorClient.GetStream();
                nsS.ReadTimeout = 5000; // 5 segundos de timeout

                // Registar sensor
                _sensorManager.RegisterSensor(sensorId, remoteEndPoint.Split(':')[0], 0);

                byte[] buf = new byte[1024];
                bool connected = true;
                List<string> capabilities = new List<string>();

                while (connected)
                {
                    try
                    {
                        int n = nsS.Read(buf, 0, buf.Length);
                        if (n <= 0)
                        {
                            Console.WriteLine($"[INFO] Sensor {sensorId} desconectado.");
                            connected = false;
                            break;
                        }

                        string msg = Encoding.UTF8.GetString(buf, 0, n);
                        Console.WriteLine($"[DEBUG] Gateway recebeu de {sensorId}: {msg}");

                        string response = ProcessMessage(msg, sensorId, ref capabilities);
                        if (response != null)
                        {
                            nsS.Write(Enc(response));
                            Console.WriteLine($"[DEBUG] Gateway respondeu a {sensorId}: {response}");

                            // Se END, encerrar
                            if (msg == "END")
                            {
                                connected = false;
                            }
                        }
                    }
                    catch (IOException ioex)
                    {
                        Console.WriteLine($"[ERROR] Timeout ou erro de leitura para {sensorId}: {ioex.Message}");
                        _sensorManager.IncrementErrorCount(sensorId);
                        connected = false;
                    }
                }

                // Marcar como desconectado
                _sensorManager.DisconnectSensor(sensorId);
                Console.WriteLine($"[INFO] Sensor {sensorId} removido do registo.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao processar sensor {sensorId}: {ex.Message}");
            _sensorManager.DisconnectSensor(sensorId);
        }
    }

    /// <summary>
    /// Processa mensagem recebida de um sensor
    /// </summary>
    static string ProcessMessage(string msg, string sensorId, ref List<string> capabilities)
    {
        try
        {
            if (msg == "INIT")
            {
                Console.WriteLine($"[INFO] {sensorId} iniciou conexão");
                return "ACK_INIT";
            }
            else if (msg.StartsWith("CAPABILITIES:"))
            {
                var parts = msg.Split(':');
                if (parts.Length > 1)
                {
                    capabilities.Clear();
                    for (int i = 1; i < parts.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(parts[i]))
                        {
                            capabilities.Add(parts[i].Trim());
                        }
                    }
                    _sensorManager.UpdateCapabilities(sensorId, capabilities);
                    Console.WriteLine($"[INFO] {sensorId} declarou capacidades: {string.Join(", ", capabilities)}");
                    return "ACK_CAPABILITIES";
                }
                return "NACK_CAPABILITIES:INVALID_FORMAT";
            }
            else if (msg.StartsWith("DATA:"))
            {
                // Usar DataValidator para validação completa
                if (!_validator.ValidateDataMessage(msg, capabilities, out string type, out double value, out string error))
                {
                    Console.WriteLine($"[ERROR] {sensorId} enviou DATA inválido: {error}");
                    return $"NACK_DATA:{error.Replace(" ", "_")}";
                }

                _sensorManager.UpdateLastDataTime(sensorId);
                Console.WriteLine($"[INFO] {sensorId} enviou {type}={value}");

                // Pré-processar leitura
                string unit = _validator.GetDefaultUnit(type);
                var reading = _preprocessor.PreprocessReading(sensorId, type, value, unit);
                
                // Armazenar em ficheiro
                _fileManager.AppendRawRecord(reading);

                // Enviar para servidor
                try
                {
                    SendToServer(msg, sensorId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Falha ao enviar para servidor: {ex.Message}");
                    _sensorManager.IncrementErrorCount(sensorId);
                }

                return "ACK_DATA";
            }
            else if (msg == "END")
            {
                Console.WriteLine($"[INFO] {sensorId} finalizou conexão");
                return "ACK_END";
            }
            else
            {
                Console.WriteLine($"[ERROR] {sensorId} enviou mensagem desconhecida: {msg}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao processar mensagem de {sensorId}: {ex.Message}");
            return "NACK_DATA:PROCESSING_ERROR";
        }
    }

    /// <summary>
    /// Valida se o valor está dentro do intervalo permitido
    /// </summary>
    static bool ValidateDataRange(string type, double value)
    {
        return type switch
        {
            "TEMP" => value >= -50 && value <= 50,
            "HUM" => value >= 0 && value <= 100,
            "PRESS" => value >= 300 && value <= 1100,
            "LIGHT" => value >= 0 && value <= 100000,
            "CO2" => value >= 0 && value <= 5000,
            _ => true // Tipo desconhecido, aceitar
        };
    }

    /// <summary>
    /// Envia dados para o servidor
    /// </summary>
    static void SendToServer(string dataMsg, string sensorId)
    {
        try
        {
            using var serv = new TcpClient("127.0.0.1", 5002);
            serv.ReceiveTimeout = 5000;
            var nsV = serv.GetStream();
            
            // Formatar mensagem para servidor
            string storeMsg = $"STORE_DATA:{dataMsg}:GW001";
            nsV.Write(Enc(storeMsg));
            
            byte[] buf = new byte[1024];
            int m = nsV.Read(buf, 0, buf.Length);
            string response = Encoding.UTF8.GetString(buf, 0, m);
            Console.WriteLine($"[DEBUG] Servidor respondeu: {response}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao contactar servidor: {ex.Message}");
        }
    }

    /// <summary>
    /// Thread de limpeza periódica
    /// </summary>
    static void CleanupThread()
    {
        int cleanupCounter = 0;

        while (true)
        {
            try
            {
                Thread.Sleep(60000); // A cada 1 minuto
                
                Console.WriteLine($"[INFO] Limpeza de sensores inativos...");
                _sensorManager.CleanupInactiveSensors(60);
                
                var activeSensors = _sensorManager.GetActiveSensors();
                Console.WriteLine($"[INFO] Sensores activos: {activeSensors.Count}");

                // A cada 60 minutos (3600 segundos / 60 para 60 iterações), fazer limpeza de ficheiros
                cleanupCounter++;
                if (cleanupCounter >= 60)
                {
                    Console.WriteLine($"[INFO] Limpeza de ficheiros antigos (>7 dias)...");
                    _fileManager.CleanupOldFiles(7);
                    
                    var (fileCount, totalSize) = _fileManager.GetFileStats();
                    Console.WriteLine($"[INFO] Estatísticas de ficheiros: {fileCount} ficheiro(s), {totalSize / 1024} KB");
                    
                    cleanupCounter = 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Erro na thread de limpeza: {ex.Message}");
            }
        }
    }

    static byte[] Enc(string s) => Encoding.UTF8.GetBytes(s);
}
