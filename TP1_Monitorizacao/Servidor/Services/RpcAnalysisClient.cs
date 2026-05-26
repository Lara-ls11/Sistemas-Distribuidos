using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Analysis;
using Grpc.Net.Client;

namespace Servidor.Services
{
    /// <summary>
    /// Cliente gRPC para o Serviço de Análise e Previsão.
    /// Invoca remotamente AnalyzeReadings, DetectPatterns e PredictHealthRisk.
    /// </summary>
    public class RpcAnalysisClient : IDisposable
    {
        private readonly GrpcChannel? _channel;
        private readonly AnalysisService.AnalysisServiceClient? _client;
        private bool _available = false;

        public RpcAnalysisClient(string host = "localhost", int port = 50052)
        {
            try
            {
                _channel   = GrpcChannel.ForAddress($"http://{host}:{port}");
                _client    = new AnalysisService.AnalysisServiceClient(_channel);
                _available = true;
                Console.WriteLine($"[RPC] AnalysisService configurado em {host}:{port}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Não foi possível ligar ao AnalysisService: {ex.Message}");
            }
        }

        public bool IsAvailable => _available;

        // ── AnalyzeReadings ───────────────────────────────────────────────────

        public async Task<AnalysisResponse?> AnalyzeReadingsAsync(
            string sensorId, string dataType, DateTime start, DateTime end)
        {
            if (!_available) return null;
            try
            {
                var req = new AnalysisRequest
                {
                    SensorId  = sensorId,
                    DataType  = dataType,
                    StartTime = start.ToString("o"),
                    EndTime   = end.ToString("o")
                };
                Console.WriteLine($"[RPC→] AnalyzeReadings({sensorId}, {dataType})");
                var resp = await _client.AnalyzeReadingsAsync(req);
                Console.WriteLine($"[RPC←] avg={resp.Average:F2} trend={resp.Trend}");
                return resp;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] RPC AnalyzeReadings: {ex.Message}");
                return null;
            }
        }

        // ── DetectPatterns ────────────────────────────────────────────────────

        public async Task<PatternResponse?> DetectPatternsAsync(
            string dataType, List<(string timestamp, double value, string sensorId)> points)
        {
            if (!_available) return null;
            try
            {
                var req = new PatternRequest { DataType = dataType };
                foreach (var (ts, val, sid) in points)
                    req.Data.Add(new DataPoint { Timestamp = ts, Value = val, SensorId = sid });

                Console.WriteLine($"[RPC→] DetectPatterns({dataType}, n={points.Count})");
                var resp = await _client.DetectPatternsAsync(req);
                Console.WriteLine($"[RPC←] {resp.Patterns.Count} padrão(ões): {resp.Summary[..Math.Min(80, resp.Summary.Length)]}");
                return resp;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] RPC DetectPatterns: {ex.Message}");
                return null;
            }
        }

        // ── PredictHealthRisk ─────────────────────────────────────────────────

        public async Task<RiskResponse?> PredictHealthRiskAsync(
            string zone, List<(string sensorId, string dataType, double value, string unit)> readings)
        {
            if (!_available) return null;
            try
            {
                var req = new RiskRequest { Zone = zone };
                foreach (var (sid, dtype, val, unit) in readings)
                    req.Readings.Add(new LatestReading
                    {
                        SensorId = sid,
                        DataType = dtype,
                        Value    = val,
                        Unit     = unit
                    });

                Console.WriteLine($"[RPC→] PredictHealthRisk(zone={zone}, n={readings.Count})");
                var resp = await _client.PredictHealthRiskAsync(req);
                Console.WriteLine($"[RPC←] risk={resp.RiskLevel} score={resp.RiskScore:F1}");
                return resp;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] RPC PredictHealthRisk: {ex.Message}");
                return null;
            }
        }

        public void Dispose() => _channel?.Dispose();
    }
}
