using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Gateway.Data;

namespace Gateway.Services
{
    /// <summary>
    /// Informações de agregação para transmissão
    /// </summary>
    public class AggregatedData
    {
        [JsonPropertyName("sensorId")]
        public string SensorId { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("period")]
        public PeriodInfo Period { get; set; }

        [JsonPropertyName("stats")]
        public StatisticsInfo Statistics { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        public class PeriodInfo
        {
            [JsonPropertyName("start")]
            public DateTime Start { get; set; }

            [JsonPropertyName("end")]
            public DateTime End { get; set; }
        }

        public class StatisticsInfo
        {
            [JsonPropertyName("average")]
            public double Average { get; set; }

            [JsonPropertyName("min")]
            public double Min { get; set; }

            [JsonPropertyName("max")]
            public double Max { get; set; }

            [JsonPropertyName("count")]
            public int Count { get; set; }
        }
    }

    /// <summary>
    /// Serviço de agregação de dados de sensores
    /// </summary>
    public class DataAggregationService
    {
        private readonly int _aggregationIntervalMinutes = 15;

        /// <summary>
        /// Converte agregação de BD para formato de transmissão
        /// </summary>
        public AggregatedData ConvertToAggregatedData(DataAggregateEntity aggregate)
        {
            return new AggregatedData
            {
                SensorId = aggregate.SensorId,
                Type = aggregate.Type,
                Period = new AggregatedData.PeriodInfo
                {
                    Start = aggregate.PeriodStart,
                    End = aggregate.PeriodEnd
                },
                Statistics = new AggregatedData.StatisticsInfo
                {
                    Average = aggregate.Average,
                    Min = aggregate.Min,
                    Max = aggregate.Max,
                    Count = aggregate.Count
                },
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Obtém intervalo de agregação atual
        /// </summary>
        public (DateTime start, DateTime end) GetCurrentAggregationPeriod()
        {
            DateTime now = DateTime.UtcNow;
            int minutes = now.Minute;
            int roundedMinutes = (minutes / _aggregationIntervalMinutes) * _aggregationIntervalMinutes;

            DateTime start = now.Date.AddHours(now.Hour).AddMinutes(roundedMinutes);
            DateTime end = start.AddMinutes(_aggregationIntervalMinutes);

            return (start, end);
        }

        /// <summary>
        /// Obtém período para agregação baseado em timestamp
        /// </summary>
        public (DateTime start, DateTime end) GetAggregationPeriodForTimestamp(DateTime timestamp)
        {
            int minutes = timestamp.Minute;
            int roundedMinutes = (minutes / _aggregationIntervalMinutes) * _aggregationIntervalMinutes;

            DateTime start = timestamp.Date.AddHours(timestamp.Hour).AddMinutes(roundedMinutes);
            DateTime end = start.AddMinutes(_aggregationIntervalMinutes);

            return (start, end);
        }

        /// <summary>
        /// Serializa agregação para transmissão ao servidor
        /// </summary>
        public string SerializeForTransmission(AggregatedData aggregated)
        {
            return System.Text.Json.JsonSerializer.Serialize(aggregated, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            });
        }
    }
}
