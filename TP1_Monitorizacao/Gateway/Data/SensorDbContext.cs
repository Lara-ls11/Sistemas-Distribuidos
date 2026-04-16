using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace Gateway.Data
{
    /// <summary>
    /// Entidade para armazenar leituras de sensores na base de dados
    /// </summary>
    public class SensorReadingEntity
    {
        public int Id { get; set; }
        public string SensorId { get; set; }
        public string Type { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public string Quality { get; set; }
        public DateTime Timestamp { get; set; }
        public double? ZScore { get; set; }
        public bool IsOutlier { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Entidade para armazenar agregação de dados
    /// </summary>
    public class DataAggregateEntity
    {
        public int Id { get; set; }
        public string SensorId { get; set; }
        public string Type { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public double Average { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public int Count { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool SentToServer { get; set; }
    }

    /// <summary>
    /// Contexto de base de dados para gerenciar sensores e leituras
    /// </summary>
    public class SensorDbContext : DbContext
    {
        private readonly string _connectionString;

        public DbSet<SensorReadingEntity> SensorReadings { get; set; }
        public DbSet<DataAggregateEntity> DataAggregates { get; set; }

        public SensorDbContext(string dbPath = "data/sensors.db")
        {
            _connectionString = $"Data Source={dbPath}";
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite(_connectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configurar índices para melhor desempenho
            modelBuilder.Entity<SensorReadingEntity>()
                .HasIndex(r => r.SensorId);

            modelBuilder.Entity<SensorReadingEntity>()
                .HasIndex(r => r.Timestamp);

            modelBuilder.Entity<SensorReadingEntity>()
                .HasIndex(r => new { r.SensorId, r.Type, r.Timestamp });

            modelBuilder.Entity<DataAggregateEntity>()
                .HasIndex(a => a.SensorId);

            modelBuilder.Entity<DataAggregateEntity>()
                .HasIndex(a => new { a.SensorId, a.PeriodStart });

            modelBuilder.Entity<DataAggregateEntity>()
                .HasIndex(a => a.SentToServer);
        }
    }
}
