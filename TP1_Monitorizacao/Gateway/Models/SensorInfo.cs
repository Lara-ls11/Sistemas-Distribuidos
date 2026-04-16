using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Gateway.Models
{
    /// <summary>
    /// Representa informações de um sensor conectado
    /// </summary>
    public class SensorInfo
    {
        [JsonPropertyName("sensorId")]
        public string SensorId { get; set; }

        [JsonPropertyName("ipAddress")]
        public string IpAddress { get; set; }

        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("capabilities")]
        public List<string> Capabilities { get; set; } = new List<string>();

        [JsonPropertyName("lastDataTime")]
        public DateTime? LastDataTime { get; set; }

        [JsonPropertyName("connected")]
        public bool Connected { get; set; }

        [JsonPropertyName("connectionTime")]
        public DateTime ConnectionTime { get; set; }

        [JsonPropertyName("dataCount")]
        public int DataCount { get; set; }

        [JsonPropertyName("errorCount")]
        public int ErrorCount { get; set; }

        public SensorInfo()
        {
        }

        public SensorInfo(string sensorId, string ipAddress, int port)
        {
            SensorId = sensorId;
            IpAddress = ipAddress;
            Port = port;
            Connected = true;
            ConnectionTime = DateTime.UtcNow;
            DataCount = 0;
            ErrorCount = 0;
        }

        public override string ToString()
        {
            return $"Sensor {SensorId} ({IpAddress}:{Port}) - Capabilities: {string.Join(",", Capabilities)} - Connected: {Connected}";
        }
    }
}
