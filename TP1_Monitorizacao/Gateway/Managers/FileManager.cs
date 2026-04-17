using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Gateway.Services;

namespace Gateway.Managers
{
    /// <summary>
    /// Classe para armazenar leitura de sensor em ficheiro
    /// </summary>
    public class RawDataFile
    {
        [JsonPropertyName("gatewayId")]
        public string GatewayId { get; set; }

        [JsonPropertyName("fileName")]
        public string FileName { get; set; }

        [JsonPropertyName("period")]
        public PeriodInfo Period { get; set; }

        [JsonPropertyName("records")]
        public List<SensorReading> Records { get; set; } = new();

        [JsonPropertyName("recordCount")]
        public int RecordCount => Records.Count;

        [JsonPropertyName("metadata")]
        public MetadataInfo Metadata { get; set; }

        public class PeriodInfo
        {
            [JsonPropertyName("start")]
            public DateTime Start { get; set; }

            [JsonPropertyName("end")]
            public DateTime End { get; set; }
        }

        public class MetadataInfo
        {
            [JsonPropertyName("created")]
            public DateTime Created { get; set; }

            [JsonPropertyName("lastModified")]
            public DateTime LastModified { get; set; }

            [JsonPropertyName("version")]
            public string Version { get; set; } = "1.0";
        }
    }

    /// <summary>
    /// Gerencia armazenamento de dados em ficheiros
    /// </summary>
    public class FileManager
    {
        private readonly string _baseDataDir = "data";
        private readonly string _rawDataDir = "data/raw";
        private readonly string _logDir = "data/logs";
        private readonly string _gatewayId = "GW001";
        private readonly int _rotationIntervalMinutes = 15;

        private readonly Dictionary<string, Mutex> _fileMutexes = new Dictionary<string, Mutex>();
        private readonly object _mutexDictLock = new object();

        private Mutex GetFileMutex(string filePath)
        {
            string normalizedPath = Path.GetFullPath(filePath).ToLowerInvariant();
            lock (_mutexDictLock)
            {
                if (!_fileMutexes.TryGetValue(normalizedPath, out var mutex))
                {
                    mutex = new Mutex();
                    _fileMutexes[normalizedPath] = mutex;
                }
                return mutex;
            }
        }

        private Dictionary<string, RawDataFile> _fileCache = new Dictionary<string, RawDataFile>();

        public FileManager()
        {
            EnsureDirectoriesExist();
        }

        /// <summary>
        /// Cria directórios necessários
        /// </summary>
        public void EnsureDirectoriesExist()
        {
            try
            {
                if (!Directory.Exists(_baseDataDir))
                    Directory.CreateDirectory(_baseDataDir);

                if (!Directory.Exists(_rawDataDir))
                    Directory.CreateDirectory(_rawDataDir);

                if (!Directory.Exists(_logDir))
                    Directory.CreateDirectory(_logDir);

                string todayDir = Path.Combine(_rawDataDir, DateTime.UtcNow.ToString("yyyy-MM-dd"));
                if (!Directory.Exists(todayDir))
                    Directory.CreateDirectory(todayDir);

                Console.WriteLine($"[INFO] Directórios de dados criados/verificados");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Falha ao criar directórios: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtém nome do ficheiro actual baseado na hora
        /// </summary>
        public string GetCurrentDataFilePath()
        {
            DateTime now = DateTime.UtcNow;
            int minutes = now.Minute;
            int roundedMinutes = (minutes / _rotationIntervalMinutes) * _rotationIntervalMinutes;
            
            string dateDir = now.ToString("yyyy-MM-dd");
            string timeStr = $"{now:HH}-{roundedMinutes:D2}";
            string fileName = $"{_gatewayId}_{timeStr}.json";
            
            return Path.Combine(_rawDataDir, dateDir, fileName);
        }

        /// <summary>
        /// Obtém período do ficheiro atual
        /// </summary>
        private (DateTime start, DateTime end) GetCurrentPeriod()
        {
            DateTime now = DateTime.UtcNow;
            int minutes = now.Minute;
            int roundedMinutes = (minutes / _rotationIntervalMinutes) * _rotationIntervalMinutes;
            
            DateTime start = now.Date.AddHours(now.Hour).AddMinutes(roundedMinutes);
            DateTime end = start.AddMinutes(_rotationIntervalMinutes);
            
            return (start, end);
        }

        /// <summary>
        /// Adiciona uma leitura ao ficheiro actual
        /// </summary>
        public bool AppendRawRecord(SensorReading reading)
        {
            try
            {
                string filePath = GetCurrentDataFilePath();
                var (periodStart, periodEnd) = GetCurrentPeriod();

                var fileMutex = GetFileMutex(filePath);
                fileMutex.WaitOne();

                try
                {
                    // Carregar ou criar ficheiro
                    RawDataFile dataFile;
                    if (File.Exists(filePath))
                    {
                        dataFile = ReadRawDataFile(filePath);
                        if (dataFile == null)
                        {
                            dataFile = CreateNewRawDataFile(filePath, periodStart, periodEnd);
                        }
                    }
                    else
                    {
                        dataFile = CreateNewRawDataFile(filePath, periodStart, periodEnd);
                    }

                    // Adicionar leitura
                    dataFile.Records.Add(reading);
                    dataFile.Metadata.LastModified = DateTime.UtcNow;

                    // Guardar ficheiro
                    SaveRawDataFile(filePath, dataFile);

                    Console.WriteLine($"[DEBUG] Leitura armazenada: {reading.SensorId}/{reading.Type}={reading.Value}");
                    return true;
                }
                finally
                {
                    fileMutex.ReleaseMutex();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Falha ao armazenar leitura: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Cria novo ficheiro de dados brutos
        /// </summary>
        private RawDataFile CreateNewRawDataFile(string filePath, DateTime periodStart, DateTime periodEnd)
        {
            var fileName = Path.GetFileName(filePath);
            return new RawDataFile
            {
                GatewayId = _gatewayId,
                FileName = fileName,
                Period = new RawDataFile.PeriodInfo
                {
                    Start = periodStart,
                    End = periodEnd
                },
                Records = new List<SensorReading>(),
                Metadata = new RawDataFile.MetadataInfo
                {
                    Created = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow,
                    Version = "1.0"
                }
            };
        }

        /// <summary>
        /// Lê ficheiro de dados brutos
        /// </summary>
        private RawDataFile ReadRawDataFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                string json = File.ReadAllText(filePath);
                var dataFile = JsonSerializer.Deserialize<RawDataFile>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return dataFile;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Falha ao ler ficheiro {filePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Guarda ficheiro de dados brutos
        /// </summary>
        private void SaveRawDataFile(string filePath, RawDataFile dataFile)
        {
            try
            {
                // Garantir que directório existe
                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonSerializer.Serialize(dataFile, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Falha ao guardar ficheiro {filePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Lê registos de um dia específico
        /// </summary>
        public List<SensorReading> ReadDayRecords(DateTime date, string sensorId = null, string type = null)
        {
            try
            {
                var records = new List<SensorReading>();
                string dayDir = Path.Combine(_rawDataDir, date.ToString("yyyy-MM-dd"));

                if (!Directory.Exists(dayDir))
                    return records;

                var files = Directory.GetFiles(dayDir, $"{_gatewayId}_*.json");

                foreach (var filePath in files)
                {
                    var fileMutex = GetFileMutex(filePath);
                    fileMutex.WaitOne();

                    RawDataFile dataFile;
                    try
                    {
                        dataFile = ReadRawDataFile(filePath);
                    }
                    finally
                    {
                        fileMutex.ReleaseMutex();
                    }

                    if (dataFile != null)
                    {
                        var dayRecords = dataFile.Records;

                        // Filtrar por sensor ID se especificado
                        if (!string.IsNullOrEmpty(sensorId))
                            dayRecords = dayRecords.Where(r => r.SensorId == sensorId).ToList();

                        // Filtrar por tipo se especificado
                        if (!string.IsNullOrEmpty(type))
                            dayRecords = dayRecords.Where(r => r.Type == type).ToList();

                        records.AddRange(dayRecords);
                    }
                }

                return records;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Falha ao ler registos do dia: {ex.Message}");
                return new List<SensorReading>();
            }
        }

        /// <summary>
        /// Limpeza de ficheiros antigos
        /// </summary>
        public void CleanupOldFiles(int daysToKeep = 7)
        {
            try
            {
                DateTime cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
                int removedCount = 0;

                if (!Directory.Exists(_rawDataDir))
                    return;

                var dayDirs = Directory.GetDirectories(_rawDataDir);

                foreach (var dayDir in dayDirs)
                {
                    string dirName = Path.GetFileName(dayDir);
                    if (DateTime.TryParseExact(dirName, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime dirDate))
                    {
                        if (dirDate < cutoffDate)
                        {
                            try
                            {
                                Directory.Delete(dayDir, true);
                                removedCount++;
                                Console.WriteLine($"[INFO] Directório removido: {dirName}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ERROR] Falha ao remover directório {dirName}: {ex.Message}");
                            }
                        }
                    }
                }

                if (removedCount > 0)
                {
                    Console.WriteLine($"[INFO] Limpeza completa: {removedCount} directório(s) removido(s)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Erro durante limpeza: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtém estatísticas de ficheiros
        /// </summary>
        public (int fileCount, long totalSize) GetFileStats(DateTime? date = null)
        {
            try
            {
                int fileCount = 0;
                long totalSize = 0;

                string searchDir = _rawDataDir;
                if (date.HasValue)
                    searchDir = Path.Combine(_rawDataDir, date.Value.ToString("yyyy-MM-dd"));

                if (!Directory.Exists(searchDir))
                    return (0, 0);

                var files = Directory.GetFiles(searchDir, "*.json", SearchOption.AllDirectories);
                fileCount = files.Length;
                totalSize = files.Sum(f => new FileInfo(f).Length);

                return (fileCount, totalSize);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Falha ao obter estatísticas: {ex.Message}");
                return (0, 0);
            }
        }

        /// <summary>
        /// Obter lista de ficheiros por período
        /// </summary>
        public List<string> GetFilesByPeriod(DateTime startDate, DateTime endDate)
        {
            var files = new List<string>();

            try
            {
                for (DateTime date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    string dayDir = Path.Combine(_rawDataDir, date.ToString("yyyy-MM-dd"));
                    if (Directory.Exists(dayDir))
                    {
                        var dayFiles = Directory.GetFiles(dayDir, "*.json");
                        files.AddRange(dayFiles);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Falha ao listar ficheiros: {ex.Message}");
            }

            return files;
        }
    }
}
