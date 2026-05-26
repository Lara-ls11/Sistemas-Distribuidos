using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace Servidor.Data
{
    // ── Entidade: leitura de sensor recebida pelo Servidor ───────────────────
    public class SensorReadingEntity
    {
        public int      Id        { get; set; }
        public string   SensorId  { get; set; } = "";
        public string   Zone      { get; set; } = "";
        public string   Type      { get; set; } = "";
        public double   Value     { get; set; }
        public string   Unit      { get; set; } = "";
        public string   Quality   { get; set; } = "GOOD";
        public bool     IsOutlier { get; set; }
        public double?  ZScore    { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime ReceivedAt { get; set; }
        public string   Source    { get; set; } = "RAW"; // RAW | AGG
    }

    // ── Entidade: resultado de análise RPC ────────────────────────────────────
    public class AnalysisResultEntity
    {
        public int      Id           { get; set; }
        public string   SensorId     { get; set; } = "";
        public string   DataType     { get; set; } = "";
        public string   AnalysisType { get; set; } = ""; // STATS | PATTERNS | RISK
        public string   ResultJson   { get; set; } = ""; // JSON serializado
        public DateTime StartPeriod  { get; set; }
        public DateTime EndPeriod    { get; set; }
        public DateTime CreatedAt    { get; set; }
        public string   Zone         { get; set; } = "";
    }

    // ── Contexto EF Core ─────────────────────────────────────────────────────
    public class ServerDbContext : DbContext
    {
        private readonly string _connectionString;

        public DbSet<SensorReadingEntity>  SensorReadings  { get; set; } = null!;
        public DbSet<AnalysisResultEntity> AnalysisResults { get; set; } = null!;

        public ServerDbContext(string dbPath = "data/server.db")
        {
            _connectionString = $"Data Source={dbPath}";
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dbPath)!);
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite(_connectionString);

        protected override void OnModelCreating(ModelBuilder m)
        {
            base.OnModelCreating(m);

            m.Entity<SensorReadingEntity>()
                .HasIndex(r => r.SensorId);
            m.Entity<SensorReadingEntity>()
                .HasIndex(r => r.Timestamp);
            m.Entity<SensorReadingEntity>()
                .HasIndex(r => new { r.SensorId, r.Type, r.Timestamp });
            m.Entity<SensorReadingEntity>()
                .HasIndex(r => r.Zone);

            m.Entity<AnalysisResultEntity>()
                .HasIndex(a => a.SensorId);
            m.Entity<AnalysisResultEntity>()
                .HasIndex(a => a.CreatedAt);
        }
    }
}
