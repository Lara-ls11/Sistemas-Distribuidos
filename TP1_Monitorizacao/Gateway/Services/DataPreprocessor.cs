using System;
using System.Collections.Generic;
using System.Linq;

namespace Gateway.Services
{
    /// <summary>
    /// Representa uma leitura de sensor com metadados
    /// </summary>
    public class SensorReading
    {
        public string SensorId { get; set; }
        public string Type { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public string Quality { get; set; } = "GOOD";
        public DateTime Timestamp { get; set; }
        public double? ZScore { get; set; }
        public bool IsOutlier { get; set; }

        public override string ToString()
        {
            return $"{Type}={Value}{Unit} ({Quality}) [{Timestamp:yyyy-MM-dd HH:mm:ss}]";
        }
    }

    /// <summary>
    /// Processa e pré-trata dados recebidos de sensores
    /// </summary>
    public class DataPreprocessor
    {
        private readonly Dictionary<string, List<double>> _sensorHistory;
        private readonly int _maxHistorySize = 100;

        public DataPreprocessor()
        {
            _sensorHistory = new Dictionary<string, List<double>>();
        }

        /// <summary>
        /// Cria uma leitura estruturada a partir dos dados brutos
        /// </summary>
        public SensorReading CreateReading(string sensorId, string type, double value, string unit, string quality = "GOOD")
        {
            return new SensorReading
            {
                SensorId = sensorId,
                Type = type,
                Value = value,
                Unit = unit,
                Quality = quality,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Adiciona uma leitura ao histórico
        /// </summary>
        public void AddToHistory(string sensorId, string type, double value)
        {
            string key = $"{sensorId}:{type}";
            
            if (!_sensorHistory.ContainsKey(key))
            {
                _sensorHistory[key] = new List<double>();
            }

            _sensorHistory[key].Add(value);

            // Manter tamanho máximo
            if (_sensorHistory[key].Count > _maxHistorySize)
            {
                _sensorHistory[key].RemoveAt(0);
            }
        }

        /// <summary>
        /// Obtém histórico de leituras
        /// </summary>
        public List<double> GetHistory(string sensorId, string type)
        {
            string key = $"{sensorId}:{type}";
            if (_sensorHistory.ContainsKey(key))
            {
                return new List<double>(_sensorHistory[key]);
            }
            return new List<double>();
        }

        /// <summary>
        /// Calcula estatísticas do histórico
        /// </summary>
        public SensorStatistics CalculateStatistics(string sensorId, string type)
        {
            var history = GetHistory(sensorId, type);
            
            if (history.Count == 0)
                return null;

            var stats = new SensorStatistics
            {
                Count = history.Count,
                Average = history.Average(),
                Minimum = history.Min(),
                Maximum = history.Max(),
                Sum = history.Sum()
            };

            // Calcular desvio padrão
            if (history.Count > 1)
            {
                double variance = history.Average(x => Math.Pow(x - stats.Average, 2));
                stats.StandardDeviation = Math.Sqrt(variance);
            }
            else
            {
                stats.StandardDeviation = 0;
            }

            return stats;
        }

        /// <summary>
        /// Detecta se um valor é um outlier usando Z-score
        /// </summary>
        public (bool isOutlier, double zScore) DetectOutlier(string sensorId, string type, double value, double zScoreThreshold = 3.0)
        {
            var stats = CalculateStatistics(sensorId, type);
            
            if (stats == null || stats.StandardDeviation == 0)
            {
                return (false, 0);
            }

            double zScore = (value - stats.Average) / stats.StandardDeviation;
            bool isOutlier = Math.Abs(zScore) > zScoreThreshold;

            return (isOutlier, zScore);
        }

        /// <summary>
        /// Determina qualidade da leitura baseada em Z-score
        /// </summary>
        public string DetermineQuality(string sensorId, string type, double value)
        {
            var (isOutlier, zScore) = DetectOutlier(sensorId, type, value, 3.0);

            if (isOutlier)
                return "POOR";

            var (isModerateOutlier, _) = DetectOutlier(sensorId, type, value, 2.0);
            if (isModerateOutlier)
                return "FAIR";

            return "GOOD";
        }

        /// <summary>
        /// Pré-processa uma leitura completa
        /// </summary>
        public SensorReading PreprocessReading(string sensorId, string type, double value, string unit, string providedQuality = null)
        {
            // Criar leitura
            var reading = CreateReading(sensorId, type, value, unit, providedQuality ?? "GOOD");

            // Adicionar ao histórico
            AddToHistory(sensorId, type, value);

            // Recalcular qualidade baseada em Z-score
            var detectedQuality = DetermineQuality(sensorId, type, value);
            
            // Se qualidade foi fornecida, usar a mais conservadora
            if (!string.IsNullOrEmpty(providedQuality))
            {
                reading.Quality = CombineQuality(providedQuality, detectedQuality);
            }
            else
            {
                reading.Quality = detectedQuality;
            }

            // Detectar outlier
            var (isOutlier, zScore) = DetectOutlier(sensorId, type, value);
            reading.IsOutlier = isOutlier;
            reading.ZScore = zScore;

            return reading;
        }

        /// <summary>
        /// Combina duas qualidades (usa a mais conservadora)
        /// </summary>
        private string CombineQuality(string quality1, string quality2)
        {
            var qualityRank = new Dictionary<string, int>
            {
                { "GOOD", 0 },
                { "FAIR", 1 },
                { "POOR", 2 }
            };

            int rank1 = qualityRank.ContainsKey(quality1) ? qualityRank[quality1] : 0;
            int rank2 = qualityRank.ContainsKey(quality2) ? qualityRank[quality2] : 0;

            return rank1 >= rank2 ? quality1 : quality2;
        }

        /// <summary>
        /// Limpa histórico de um sensor
        /// </summary>
        public void ClearHistory(string sensorId, string type = null)
        {
            if (type == null)
            {
                // Limpar todo o histórico do sensor
                var keysToRemove = _sensorHistory.Keys
                    .Where(k => k.StartsWith($"{sensorId}:"))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _sensorHistory.Remove(key);
                }
            }
            else
            {
                // Limpar histórico de um tipo específico
                string key = $"{sensorId}:{type}";
                if (_sensorHistory.ContainsKey(key))
                {
                    _sensorHistory.Remove(key);
                }
            }
        }
    }

    /// <summary>
    /// Estatísticas de um tipo de leitura
    /// </summary>
    public class SensorStatistics
    {
        public int Count { get; set; }
        public double Average { get; set; }
        public double Minimum { get; set; }
        public double Maximum { get; set; }
        public double Sum { get; set; }
        public double StandardDeviation { get; set; }

        public override string ToString()
        {
            return $"Count={Count}, Avg={Average:F2}, Min={Minimum:F2}, Max={Maximum:F2}, StdDev={StandardDeviation:F2}";
        }
    }
}
