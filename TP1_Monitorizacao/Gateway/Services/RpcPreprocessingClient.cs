using System;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Preprocessing;

namespace Gateway.Services
{
    /// <summary>
    /// Cliente gRPC para o Serviço de Pré-processamento.
    /// Invoca remotamente PreprocessData e ConvertFormat.
    /// </summary>
    public class RpcPreprocessingClient : IDisposable
    {
        private readonly GrpcChannel _channel;
        private readonly PreprocessingService.PreprocessingServiceClient _client;
        private bool _available = false;

        public RpcPreprocessingClient(string host = "localhost", int port = 50051)
        {
            try
            {
                _channel = GrpcChannel.ForAddress($"http://{host}:{port}");
                _client  = new PreprocessingService.PreprocessingServiceClient(_channel);
                _available = true;
                Console.WriteLine($"[RPC] PreprocessingService configurado em {host}:{port}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Não foi possível ligar ao PreprocessingService: {ex.Message}");
            }
        }

        /// <summary>
        /// Pré-processa uma leitura de sensor via RPC.
        /// Se o serviço não estiver disponível, faz fallback local.
        /// </summary>
        public async Task<ProcessedReading?> PreprocessDataAsync(
            string sensorId, string dataType, double value, string unit, string format = "JSON")
        {
            if (!_available)
            {
                Console.WriteLine("[WARN] PreprocessingService indisponível – a usar fallback local.");
                return LocalFallback(sensorId, dataType, value, unit);
            }

            try
            {
                var request = new RawReading
                {
                    SensorId  = sensorId,
                    DataType  = dataType,
                    Value     = value,
                    Unit      = unit,
                    Format    = format
                };

                Console.WriteLine($"[RPC→] PreprocessData({sensorId}, {dataType}, {value})");
                var response = await _client.PreprocessDataAsync(request);
                Console.WriteLine($"[RPC←] quality={response.Quality} outlier={response.IsOutlier} z={response.ZScore:F2}");
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] RPC PreprocessData falhou: {ex.Message}. Fallback local.");
                return LocalFallback(sensorId, dataType, value, unit);
            }
        }

        /// <summary>
        /// Converte formato de dados via RPC (JSON, XML, CSV → JSON normalizado).
        /// </summary>
        public async Task<string?> ConvertFormatAsync(string rawData, string sourceFormat)
        {
            if (!_available) return rawData;

            try
            {
                var request = new FormatRequest
                {
                    RawData      = rawData,
                    SourceFormat = sourceFormat
                };

                var response = await _client.ConvertFormatAsync(request);
                return response.Success ? response.NormalizedJson : rawData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] RPC ConvertFormat falhou: {ex.Message}");
                return rawData;
            }
        }

        /// <summary>
        /// Fallback local simples quando o serviço RPC não está acessível.
        /// </summary>
        private ProcessedReading LocalFallback(string sensorId, string dataType, double value, string unit)
        {
            return new ProcessedReading
            {
                SensorId       = sensorId,
                DataType       = dataType,
                NormalizedValue = value,
                NormalizedUnit  = unit,
                Quality        = "GOOD",
                IsOutlier      = false,
                ZScore         = 0.0,
                Timestamp      = DateTime.UtcNow.ToString("o"),
                Status         = "OK"
            };
        }

        public void Dispose()
        {
            _channel?.Dispose();
        }
    }
}
