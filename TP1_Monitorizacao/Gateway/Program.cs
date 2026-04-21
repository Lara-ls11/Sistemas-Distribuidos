using System; 
using System.Net; 
using System.Net.Sockets; 
using System.Text; 
using System.Collections.Generic; 
using System.Threading; 
using System.Threading.Tasks; 
using System.IO; 
using Gateway.Managers; 
using Gateway.Services; 
class Program
{
    private static SensorManager _sensorManager = new SensorManager();
    private static FileManager _fileManager = new FileManager(); 
    private static DatabaseManager _databaseManager = new DatabaseManager();
    private static DataValidator _validator = new DataValidator(); 
    private static DataPreprocessor _preprocessor = new DataPreprocessor(); 
    private static DataAggregationService _aggregationService = new DataAggregationService(); 
    private static ServerForwarderService _serverForwarder = new ServerForwarderService(); 
    private static int _sensorCounter = 0; 
    private static readonly object _counterLock = new object(); 

    private static Dictionary<string, List<string>> _sensorCapabilities = new Dictionary<string, List<string>>(); 
    private static readonly object _capabilitiesLock = new object(); 

    static void Main()
    {
        Console.WriteLine("[INFO] ========================================");
        Console.WriteLine("[INFO] Funcionalidade de Operação SENSOR");
        Console.WriteLine("[INFO] Gateway - Monitorização Distribuída");
        Console.WriteLine("[INFO] ========================================");

        var gw = new TcpListener(IPAddress.Any, 5001);  
        gw.Start(); 
        Console.WriteLine("[INFO] Gateway iniciado na porta 5001"); 
        Console.WriteLine("[INFO] À espera de sensores...");

        _serverForwarder.TestConnection(); 

        new Thread(() => CleanupThread()) { IsBackground = true }.Start(); 
        new Thread(() => AggregationThread()) { IsBackground = true }.Start(); 

        while (true) 
        {
            try 
            {
                var sensor = gw.AcceptTcpClient(); 
                var remoteEndPoint = sensor.Client.RemoteEndPoint.ToString(); 

                string sensorId; 
                lock (_counterLock) 
                {
                    _sensorCounter++; 
                    sensorId = $"SENSOR_{_sensorCounter:000}"; 
                }

                Console.WriteLine($"[INFO] Sensor conectado: {remoteEndPoint} (ID: {sensorId})");

                new Thread(() => HandleSensor(sensor, sensorId, remoteEndPoint)) { IsBackground = true }.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Erro ao aceitar conexão: {ex.Message}");
            }
        }
    }

    static void HandleSensor(TcpClient sensorClient, string sensorId, string remoteEndPoint)
    {
        try
        {
            using (sensorClient)
            {
                var nsS = sensorClient.GetStream();
                nsS.ReadTimeout = Timeout.Infinite;

                var reader = new StreamReader(nsS, Encoding.UTF8);

                _sensorManager.RegisterSensor(sensorId, remoteEndPoint.Split(':')[0], 0);

                lock (_capabilitiesLock)
                {
                    _sensorCapabilities[sensorId] = new List<string>();
                }

                bool connected = true;

                while (connected)
                {
                    try
                    {
                        string msg = reader.ReadLine();

                        if (msg == null)
                        {
                            Console.WriteLine($"[INFO] Sensor {sensorId} desconectado.");
                            connected = false;
                            break;
                        }

                        msg = msg.Trim();
                        Console.WriteLine($"[DEBUG] Gateway recebeu de {sensorId}: {msg}");

                        string response = ProcessMessage(msg, sensorId);
                        if (response != null)
                        {
                            nsS.Write(Enc(response));
                            Console.WriteLine($"[DEBUG] Gateway respondeu a {sensorId}: {response}");

                            if (msg == "END")
                                connected = false;
                        }
                    }
                    catch (IOException ioex)
                    {
                        Console.WriteLine($"[ERROR] Timeout ou erro de leitura para {sensorId}: {ioex.Message}");
                        _sensorManager.IncrementErrorCount(sensorId);
                        connected = false;
                    }
                }

                _sensorManager.DisconnectSensor(sensorId);

                lock (_capabilitiesLock)
                {
                    _sensorCapabilities.Remove(sensorId);
                }

                Console.WriteLine($"[INFO] Sensor {sensorId} removido do registo.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao processar sensor {sensorId}: {ex.Message}");
            _sensorManager.DisconnectSensor(sensorId);
        }
    }

    static string ProcessMessage(string msg, string sensorId)
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
                Console.WriteLine($"[DEBUG] Recebida mensagem CAPABILITIES: '{msg}'");

                string caps = msg.Substring("CAPABILITIES:".Length);
                Console.WriteLine($"[DEBUG] Após remover prefixo: '{caps}'");

                var parts = caps.Split(new char[] { ',', ':' }, StringSplitOptions.RemoveEmptyEntries);
                Console.WriteLine($"[DEBUG] Partes encontradas: {parts.Length}");

                lock (_capabilitiesLock)
                {
                    _sensorCapabilities[sensorId].Clear();

                    foreach (var cap in parts)
                    {
                        string trimmedCap = cap.Trim().ToUpper();
                        Console.WriteLine($"[DEBUG] Adicionando capability: '{trimmedCap}'");
                        _sensorCapabilities[sensorId].Add(trimmedCap);
                    }

                    Console.WriteLine($"[DEBUG] Lista final de capabilities: {string.Join(", ", _sensorCapabilities[sensorId])}");
                    _sensorManager.UpdateCapabilities(sensorId, _sensorCapabilities[sensorId]);
                    Console.WriteLine($"[INFO] {sensorId} declarou capacidades: {string.Join(", ", _sensorCapabilities[sensorId])}");
                }

                return "ACK_CAPABILITIES";
            }
            else if (msg.StartsWith("DATA:"))
            {
                List<string> capabilities = new List<string>();
                lock (_capabilitiesLock)
                {
                    if (_sensorCapabilities.ContainsKey(sensorId))
                    {
                        capabilities = new List<string>(_sensorCapabilities[sensorId]);
                    }
                }

                Console.WriteLine($"[DEBUG] ===== VALIDAÇÃO DE DATA =====");
                Console.WriteLine($"[DEBUG] Mensagem recebida: '{msg}'");
                Console.WriteLine($"[DEBUG] SensorId: {sensorId}");
                Console.WriteLine($"[DEBUG] Capabilities guardadas: {string.Join(", ", _sensorCapabilities.Keys)}");
                Console.WriteLine($"[DEBUG] Capabilities actuais para este sensor: {(capabilities.Count > 0 ? string.Join(", ", capabilities) : "NENHUMA!")}");
                foreach (var cap in capabilities)
                {
                    Console.WriteLine($"[DEBUG]   - Cap: '{cap}' (length: {cap.Length})");
                }

                if (!_validator.ValidateDataMessage(msg, capabilities, out string type, out double value, out string error))
                {
                    Console.WriteLine($"[ERROR] {sensorId} enviou DATA inválido: {error}");
                    return $"NACK_DATA:{error.Replace(" ", "_")}";
                }

                _sensorManager.UpdateLastDataTime(sensorId);
                Console.WriteLine($"[INFO] {sensorId} enviou {type}={value}");

                string unit = _validator.GetDefaultUnit(type);
                var reading = _preprocessor.PreprocessReading(sensorId, type, value, unit);

                _fileManager.AppendRawRecord(reading);
                Console.WriteLine($"[DEBUG] Leitura armazenada em ficheiro: {sensorId}/{type}={value}{unit}");

                _databaseManager.InsertSensorReading(reading);
                Console.WriteLine($"[DEBUG] Leitura persistida em BD: {sensorId}/{type}");

                try
                {
                    _serverForwarder.SendRawData(sensorId, type, value, unit, reading.Timestamp);
                    Console.WriteLine($"[DEBUG] Dados encaminhados para servidor: {sensorId}/{type}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Falha ao encaminhar para servidor: {ex.Message}");
                    _sensorManager.IncrementErrorCount(sensorId);
                }

                return "ACK_DATA";
            }
            else if (msg == "END")
            {
                Console.WriteLine($"[INFO] {sensorId} finalizou conexão");
                return "ACK_END";
            }
            // aqui falta o heartbeat
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

    static void AggregationThread()
    {
        // (mantém igual)
    }

    static void CleanupThread()
    {
        // (mantém igual)
    }

    static byte[] Enc(string s) => Encoding.UTF8.GetBytes(s + "\n");
}
