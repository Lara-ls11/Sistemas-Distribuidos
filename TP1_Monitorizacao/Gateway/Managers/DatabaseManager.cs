using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gateway.Data;
using Gateway.Services;

namespace Gateway.Managers
{
    /// <summary>
    /// Gerencia persistência de dados em base de dados relacional
    /// </summary>
    public class DatabaseManager
    {
        private readonly SensorDbContext _dbContext;
        private readonly object _lockObject = new object();

        public DatabaseManager(string dbPath = "data/sensors.db")
        {
            _dbContext = new SensorDbContext(dbPath);
        }

        /// <summary>
        /// Adiciona uma leitura de sensor à base de dados
        /// </summary>
        public bool InsertSensorReading(SensorReading reading)
        {
            lock (_lockObject)
            {
                try
                {
                    var entity = new SensorReadingEntity
                    {
                        SensorId = reading.SensorId,
                        Type = reading.Type,
                        Value = reading.Value,
                        Unit = reading.Unit,
                        Quality = reading.Quality,
                        Timestamp = reading.Timestamp,
                        ZScore = reading.ZScore,
                        IsOutlier = reading.IsOutlier,
                        CreatedAt = DateTime.UtcNow
                    };

                    _dbContext.SensorReadings.Add(entity);
                    _dbContext.SaveChanges();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Falha ao inserir leitura na BD: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Obtém leituras de um sensor dentro de um período
        /// </summary>
        public List<SensorReadingEntity> GetSensorReadings(string sensorId, DateTime startTime, DateTime endTime)
        {
            lock (_lockObject)
            {
                try
                {
                    return _dbContext.SensorReadings
                        .Where(r => r.SensorId == sensorId && r.Timestamp >= startTime && r.Timestamp <= endTime)
                        .OrderBy(r => r.Timestamp)
                        .ToList();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Falha ao recuperar leituras: {ex.Message}");
                    return new List<SensorReadingEntity>();
                }
            }
        }

        /// <summary>
        /// Calcula agregação de dados para um período
        /// </summary>
        public DataAggregateEntity CalculateAggregate(string sensorId, string type, DateTime periodStart, DateTime periodEnd)
        {
            lock (_lockObject)
            {
                try
                {
                    var readings = _dbContext.SensorReadings
                        .Where(r => r.SensorId == sensorId && r.Type == type && 
                                   r.Timestamp >= periodStart && r.Timestamp <= periodEnd)
                        .ToList();

                    if (readings.Count == 0)
                        return null;

                    var aggregate = new DataAggregateEntity
                    {
                        SensorId = sensorId,
                        Type = type,
                        PeriodStart = periodStart,
                        PeriodEnd = periodEnd,
                        Average = readings.Average(r => r.Value),
                        Min = readings.Min(r => r.Value),
                        Max = readings.Max(r => r.Value),
                        Count = readings.Count,
                        CreatedAt = DateTime.UtcNow,
                        SentToServer = false
                    };

                    _dbContext.DataAggregates.Add(aggregate);
                    _dbContext.SaveChanges();
                    return aggregate;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Falha ao calcular agregação: {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Obtém agregações não enviadas para o servidor
        /// </summary>
        public List<DataAggregateEntity> GetPendingAggregates()
        {
            lock (_lockObject)
            {
                try
                {
                    return _dbContext.DataAggregates
                        .Where(a => !a.SentToServer)
                        .OrderBy(a => a.CreatedAt)
                        .ToList();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Falha ao recuperar agregações pendentes: {ex.Message}");
                    return new List<DataAggregateEntity>();
                }
            }
        }

        /// <summary>
        /// Marca agregação como enviada para o servidor
        /// </summary>
        public bool MarkAggregateAsSent(int aggregateId)
        {
            lock (_lockObject)
            {
                try
                {
                    var aggregate = _dbContext.DataAggregates.Find(aggregateId);
                    if (aggregate != null)
                    {
                        aggregate.SentToServer = true;
                        _dbContext.SaveChanges();
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Falha ao marcar agregação como enviada: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Obtém estatísticas gerais de um sensor
        /// </summary>
        public (int totalReadings, DateTime? firstReading, DateTime? lastReading) GetSensorStatistics(string sensorId)
        {
            lock (_lockObject)
            {
                try
                {
                    var readings = _dbContext.SensorReadings
                        .Where(r => r.SensorId == sensorId)
                        .ToList();

                    if (readings.Count == 0)
                        return (0, null, null);

                    return (
                        readings.Count,
                        readings.Min(r => r.Timestamp),
                        readings.Max(r => r.Timestamp)
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Falha ao obter estatísticas: {ex.Message}");
                    return (0, null, null);
                }
            }
        }

        /// <summary>
        /// Limpa registos antigos (retenção de dados)
        /// </summary>
        public int CleanupOldRecords(int daysToKeep = 30)
        {
            lock (_lockObject)
            {
                try
                {
                    var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
                    var oldReadings = _dbContext.SensorReadings
                        .Where(r => r.Timestamp < cutoffDate)
                        .ToList();

                    if (oldReadings.Count > 0)
                    {
                        _dbContext.SensorReadings.RemoveRange(oldReadings);
                        _dbContext.SaveChanges();
                        Console.WriteLine($"[INFO] Removidos {oldReadings.Count} registos antigos da BD");
                    }

                    return oldReadings.Count;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Falha ao limpar registos antigos: {ex.Message}");
                    return 0;
                }
            }
        }

        /// <summary>
        /// Fecha a conexão com a base de dados
        /// </summary>
        public void Dispose()
        {
            _dbContext?.Dispose();
        }
    }
}
