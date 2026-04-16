using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.IO;
using System.Text.Json.Serialization;

namespace Gateway.Managers
{
    /// <summary>
    /// Gerencia informações sobre sensores conectados
    /// </summary>
    public class SensorManager
    {
        private readonly ConcurrentDictionary<string, Models.SensorInfo> _activeSensors;
        private readonly string _cacheDir = "cache";
        private readonly string _cacheFile = "cache/active_sensors.json";
        private readonly object _lockObject = new object();

        public SensorManager()
        {
            _activeSensors = new ConcurrentDictionary<string, Models.SensorInfo>();
            EnsureDirectoriesExist();
            LoadActiveSensors();
        }

        /// <summary>
        /// Cria diretórios necessários se não existirem
        /// </summary>
        private void EnsureDirectoriesExist()
        {
            if (!Directory.Exists(_cacheDir))
            {
                Directory.CreateDirectory(_cacheDir);
            }
        }

        /// <summary>
        /// Registra um novo sensor conectado
        /// </summary>
        public Models.SensorInfo RegisterSensor(string sensorId, string ipAddress, int port)
        {
            var sensor = new Models.SensorInfo(sensorId, ipAddress, port);
            _activeSensors.AddOrUpdate(sensorId, sensor, (key, existing) => sensor);
            SaveActiveSensors();
            return sensor;
        }

        /// <summary>
        /// Obtém informações de um sensor
        /// </summary>
        public Models.SensorInfo GetSensor(string sensorId)
        {
            _activeSensors.TryGetValue(sensorId, out var sensor);
            return sensor;
        }

        /// <summary>
        /// Atualiza as capacidades de um sensor
        /// </summary>
        public void UpdateCapabilities(string sensorId, List<string> capabilities)
        {
            if (_activeSensors.TryGetValue(sensorId, out var sensor))
            {
                // Remove duplicatas e ordena
                sensor.Capabilities = capabilities.Distinct().OrderBy(c => c).ToList();
                SaveActiveSensors();
            }
        }

        /// <summary>
        /// Atualiza o último tempo de leitura
        /// </summary>
        public void UpdateLastDataTime(string sensorId)
        {
            if (_activeSensors.TryGetValue(sensorId, out var sensor))
            {
                sensor.LastDataTime = DateTime.UtcNow;
                sensor.DataCount++;
                SaveActiveSensors();
            }
        }

        /// <summary>
        /// Incrementa contador de erros
        /// </summary>
        public void IncrementErrorCount(string sensorId)
        {
            if (_activeSensors.TryGetValue(sensorId, out var sensor))
            {
                sensor.ErrorCount++;
                SaveActiveSensors();
            }
        }

        /// <summary>
        /// Marca sensor como desconectado
        /// </summary>
        public void DisconnectSensor(string sensorId)
        {
            if (_activeSensors.TryGetValue(sensorId, out var sensor))
            {
                sensor.Connected = false;
                SaveActiveSensors();
            }
        }

        /// <summary>
        /// Remove um sensor do registo
        /// </summary>
        public void RemoveSensor(string sensorId)
        {
            _activeSensors.TryRemove(sensorId, out _);
            SaveActiveSensors();
        }

        /// <summary>
        /// Obtém lista de sensores activos
        /// </summary>
        public List<Models.SensorInfo> GetActiveSensors()
        {
            return _activeSensors.Values.Where(s => s.Connected).ToList();
        }

        /// <summary>
        /// Obtém lista de todos os sensores (activos e inativos)
        /// </summary>
        public List<Models.SensorInfo> GetAllSensors()
        {
            return _activeSensors.Values.ToList();
        }

        /// <summary>
        /// Obtém número de sensores activos
        /// </summary>
        public int GetActiveCount()
        {
            return _activeSensors.Count(x => x.Value.Connected);
        }

        /// <summary>
        /// Salva sensores activos em JSON
        /// </summary>
        private void SaveActiveSensors()
        {
            lock (_lockObject)
            {
                try
                {
                    var data = new
                    {
                        gatewayId = "GW001",
                        lastUpdated = DateTime.UtcNow,
                        activeSensors = GetAllSensors()
                    };

                    var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    File.WriteAllText(_cacheFile, json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Falha ao guardar active_sensors.json: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Carrega sensores activos de JSON
        /// </summary>
        private void LoadActiveSensors()
        {
            try
            {
                if (File.Exists(_cacheFile))
                {
                    var json = File.ReadAllText(_cacheFile);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("activeSensors", out var sensorsArray))
                    {
                        foreach (var sensorElement in sensorsArray.EnumerateArray())
                        {
                            var sensor = JsonSerializer.Deserialize<Models.SensorInfo>(
                                sensorElement.GetRawText(),
                                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
                            );

                            if (sensor != null)
                            {
                                sensor.Connected = false; // Marcar como desconectado ao carregar
                                _activeSensors.TryAdd(sensor.SensorId, sensor);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Falha ao carregar active_sensors.json: {ex.Message}");
            }
        }

        /// <summary>
        /// Limpa sensores inativos há mais de X minutos
        /// </summary>
        public void CleanupInactiveSensors(int inactiveMinutes = 60)
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-inactiveMinutes);
            var inactiveSensors = _activeSensors.Values
                .Where(s => !s.Connected && s.LastDataTime.HasValue && s.LastDataTime < cutoffTime)
                .ToList();

            foreach (var sensor in inactiveSensors)
            {
                RemoveSensor(sensor.SensorId);
            }
        }
    }
}
