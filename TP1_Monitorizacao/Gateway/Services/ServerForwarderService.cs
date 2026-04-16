using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Gateway.Data;

namespace Gateway.Services
{
    /// <summary>
    /// Serviço para encaminhamento de dados para o servidor
    /// </summary>
    public class ServerForwarderService
    {
        private readonly string _serverHost;
        private readonly int _serverPort;
        private readonly int _connectionTimeout = 5000; // 5 segundos

        public ServerForwarderService(string serverHost = "127.0.0.1", int serverPort = 5002)
        {
            _serverHost = serverHost;
            _serverPort = serverPort;
        }

        /// <summary>
        /// Envia dados brutos para o servidor
        /// </summary>
        public bool SendRawData(string sensorId, string type, double value, string unit, DateTime timestamp)
        {
            try
            {
                string message = $"RAW_DATA|{sensorId}|{type}|{value}|{unit}|{timestamp:O}";
                return SendToServer(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Falha ao enviar dados brutos: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Envia dados agregados para o servidor
        /// </summary>
        public bool SendAggregatedData(AggregatedData aggregated)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(aggregated, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = false
                });

                string message = $"AGG_DATA|{json}";
                return SendToServer(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Falha ao enviar dados agregados: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Envia mensagem ao servidor com tratamento de erros
        /// </summary>
        private bool SendToServer(string message)
        {
            TcpClient client = null;
            try
            {
                client = new TcpClient();

                // Conectar com timeout
                IAsyncResult connectResult = client.BeginConnect(_serverHost, _serverPort, null, null);
                if (!connectResult.AsyncWaitHandle.WaitOne(_connectionTimeout))
                {
                    client?.Close();
                    Console.WriteLine($"[WARN] Timeout ao conectar ao servidor {_serverHost}:{_serverPort}");
                    return false;
                }

                // Verificar se conectou
                if (!client.Connected)
                {
                    Console.WriteLine($"[WARN] Falha ao conectar ao servidor {_serverHost}:{_serverPort}");
                    return false;
                }

                using (var ns = client.GetStream())
                {
                    ns.WriteTimeout = 3000;
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    ns.Write(data, 0, data.Length);

                    Console.WriteLine($"[DEBUG] Mensagem enviada ao servidor: {message.Substring(0, Math.Min(100, message.Length))}...");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Erro ao enviar para servidor: {ex.Message}");
                return false;
            }
            finally
            {
                client?.Close();
            }
        }

        /// <summary>
        /// Testa conectividade com servidor
        /// </summary>
        public bool TestConnection()
        {
            TcpClient client = null;
            try
            {
                client = new TcpClient();
                IAsyncResult connectResult = client.BeginConnect(_serverHost, _serverPort, null, null);

                if (connectResult.AsyncWaitHandle.WaitOne(2000))
                {
                    if (client.Connected)
                    {
                        Console.WriteLine($"[INFO] Conexão com servidor {_serverHost}:{_serverPort} OK");
                        return true;
                    }
                }

                Console.WriteLine($"[WARN] Servidor {_serverHost}:{_serverPort} não disponível");
                return false;
            }
            catch
            {
                return false;
            }
            finally
            {
                client?.Close();
            }
        }
    }
}
