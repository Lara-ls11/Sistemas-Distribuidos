/*
 * Servidor Principal – TP2
 * ─────────────────────────────────────────────────────────────────────────────
 * • Recebe dados (RAW_DATA / AGG_DATA) dos Gateways via TCP (porta 5002)
 * • Persiste tudo em SQLite (EF Core)
 * • Invoca AnalysisService via RPC (gRPC) para análises avançadas
 * • Interface CLI para consultas e pedidos de análise
 *
 * Argumentos: [analysis_host] [analysis_port]
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Servidor.Data;
using Servidor.Services;
using Servidor.Web;

// ── Configuração ──────────────────────────────────────────────────────────────
string analysisHost = args.Length > 0 ? args[0] : "localhost";
int    analysisPort = args.Length > 1 ? int.Parse(args[1]) : 50052;
int    tcpPort      = 5002;

Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║  SERVIDOR PRINCIPAL – BD + RPC + CLI     ║");
Console.WriteLine("╚══════════════════════════════════════════╝");
Console.WriteLine($"[INFO] TCP porta      : {tcpPort}");
Console.WriteLine($"[INFO] AnalysisService: {analysisHost}:{analysisPort}");
Console.WriteLine($"[INFO] Base de dados  : data/server.db");
Console.WriteLine();

// ── Inicializar BD ────────────────────────────────────────────────────────────
var db        = new ServerDbContext("data/server.db");
var dbLock    = new SemaphoreSlim(1, 1);

// ── Cliente RPC de análise ────────────────────────────────────────────────────
using var rpcAnalysis = new RpcAnalysisClient(analysisHost, analysisPort);

// ── Iniciar Web Dashboard (porta 8080) ────────────────────────────────────────
var webServer = new WebServer(db, dbLock, rpcAnalysis, 8080);
webServer.Start();

// ── Iniciar listener TCP (compatibilidade com Gateways) ───────────────────────
var tcpListener = new TcpListener(IPAddress.Any, tcpPort);
tcpListener.Start();
Console.WriteLine($"[INFO] Servidor TCP a escutar na porta {tcpPort}...");

_ = Task.Run(async () =>
{
    while (true)
    {
        try
        {
            var client = await tcpListener.AcceptTcpClientAsync();
            _ = Task.Run(() => HandleGatewayClient(client));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao aceitar conexão TCP: {ex.Message}");
        }
    }
});

// ── Interface CLI ─────────────────────────────────────────────────────────────
await RunCliAsync();

// ═════════════════════════════════════════════════════════════════════════════
// Handler de cliente Gateway (TCP)
// ═════════════════════════════════════════════════════════════════════════════
async Task HandleGatewayClient(TcpClient client)
{
    try
    {
        using (client)
        {
            var ns      = client.GetStream();
            var reader  = new StreamReader(ns, Encoding.UTF8);
            string? msg = await reader.ReadLineAsync();

            if (string.IsNullOrWhiteSpace(msg)) return;

            msg = msg.Trim();
            Console.WriteLine($"[TCP←] {msg[..Math.Min(150, msg.Length)]}");

            if (msg.StartsWith("RAW_DATA|"))
            {
                // Formato: RAW_DATA|sensorId|type|value|unit|timestamp
                var parts = msg.Split('|');
                if (parts.Length >= 6 &&
                    double.TryParse(parts[3], System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    var reading = new SensorReadingEntity
                    {
                        SensorId   = parts[1],
                        Zone       = ExtractZone(parts[1]),
                        Type       = parts[2],
                        Value      = val,
                        Unit       = parts[4],
                        Quality    = "GOOD",
                        Timestamp  = DateTime.TryParse(parts[5], null,
                                       System.Globalization.DateTimeStyles.RoundtripKind,
                                       out var ts) ? ts : DateTime.UtcNow,
                        ReceivedAt = DateTime.UtcNow,
                        Source     = "RAW"
                    };
                    await SaveReadingAsync(reading);
                }
                await SendAckAsync(ns, "ACK");
            }
            else if (msg.StartsWith("AGG_DATA|"))
            {
                // Formato: AGG_DATA|{json}
                string json = msg.Substring("AGG_DATA|".Length);
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    string sensorId = root.TryGetProperty("sensorId", out var s) ? s.GetString()! : "?";
                    string type     = root.TryGetProperty("type",     out var t) ? t.GetString()! : "?";
                    string zone     = ExtractZone(sensorId);

                    double avg = 0;
                    if (root.TryGetProperty("stats", out var stats) &&
                        stats.TryGetProperty("average", out var avgProp))
                        avg = avgProp.GetDouble();

                    var reading = new SensorReadingEntity
                    {
                        SensorId   = sensorId,
                        Zone       = zone,
                        Type       = type,
                        Value      = avg,
                        Unit       = GetDefaultUnit(type),
                        Quality    = "GOOD",
                        Timestamp  = DateTime.UtcNow,
                        ReceivedAt = DateTime.UtcNow,
                        Source     = "AGG"
                    };
                    await SaveReadingAsync(reading);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] AGG_DATA JSON inválido: {ex.Message}");
                }
                await SendAckAsync(ns, "ACK");
            }
            else
            {
                // Formato legado TP1
                await SaveReadingAsync(new SensorReadingEntity
                {
                    SensorId   = "unknown",
                    Zone       = "?",
                    Type       = "UNKNOWN",
                    Value      = 0,
                    Unit       = "",
                    Quality    = "UNKNOWN",
                    Timestamp  = DateTime.UtcNow,
                    ReceivedAt = DateTime.UtcNow,
                    Source     = "RAW"
                });
                await SendAckAsync(ns, "ACK");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] HandleGatewayClient: {ex.Message}");
    }
}

async Task SaveReadingAsync(SensorReadingEntity reading)
{
    await dbLock.WaitAsync();
    try
    {
        db.SensorReadings.Add(reading);
        await db.SaveChangesAsync();
        Console.WriteLine($"[BD+] {reading.SensorId}/{reading.Type}={reading.Value} [{reading.Source}]");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] SaveReading: {ex.Message}");
    }
    finally
    {
        dbLock.Release();
    }
}

async Task SendAckAsync(NetworkStream ns, string msg)
{
    try
    {
        byte[] data = Encoding.UTF8.GetBytes(msg);
        await ns.WriteAsync(data);
    }
    catch { /* ignorar erros de envio de ACK */ }
}

// ═════════════════════════════════════════════════════════════════════════════
// Interface de Linha de Comandos (CLI)
// ═════════════════════════════════════════════════════════════════════════════
async Task RunCliAsync()
{
    Console.WriteLine();
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Console.WriteLine("  INTERFACE DE VISUALIZAÇÃO E ANÁLISE");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    while (true)
    {
        PrintMenu();
        string? op = Console.ReadLine()?.Trim();
        Console.WriteLine();

        switch (op)
        {
            case "1":  await CmdListSensorsAsync();              break;
            case "2":  await CmdQueryReadingsAsync();            break;
            case "3":  await CmdStatisticsLocalAsync();          break;
            case "4":  await CmdRequestAnalysisRpcAsync();       break;
            case "5":  await CmdDetectPatternsRpcAsync();        break;
            case "6":  await CmdPredictRiskRpcAsync();           break;
            case "7":  await CmdListAnalysisResultsAsync();      break;
            case "8":  await CmdExportCsvAsync();                break;
            case "0":
                Console.WriteLine("[INFO] A encerrar servidor...");
                return;
            default:
                Console.WriteLine("[WARN] Opção inválida.");
                break;
        }
    }
}

void PrintMenu()
{
    Console.WriteLine();
    Console.WriteLine("┌─────────────────────────────────────────────┐");
    Console.WriteLine("│  MENU PRINCIPAL                             │");
    Console.WriteLine("├─────────────────────────────────────────────┤");
    Console.WriteLine("│  1 - Listar sensores conhecidos             │");
    Console.WriteLine("│  2 - Consultar leituras (filtros)           │");
    Console.WriteLine("│  3 - Estatísticas locais (BD)               │");
    Console.WriteLine("│  4 - Análise estatística via RPC            │");
    Console.WriteLine("│  5 - Deteção de padrões via RPC             │");
    Console.WriteLine("│  6 - Previsão de risco de saúde via RPC     │");
    Console.WriteLine("│  7 - Ver resultados de análises anteriores  │");
    Console.WriteLine("│  8 - Exportar leituras para CSV             │");
    Console.WriteLine("│  0 - Sair                                   │");
    Console.WriteLine("└─────────────────────────────────────────────┘");
    Console.Write("Opção: ");
}

// ── 1: Listar sensores ────────────────────────────────────────────────────────
async Task CmdListSensorsAsync()
{
    await dbLock.WaitAsync();
    try
    {
        var sensors = await db.SensorReadings
            .GroupBy(r => new { r.SensorId, r.Zone })
            .Select(g => new
            {
                g.Key.SensorId,
                g.Key.Zone,
                Count        = g.Count(),
                LastReading  = g.Max(r => r.Timestamp),
                Types        = string.Join(", ", g.Select(r => r.Type).Distinct())
            })
            .OrderBy(s => s.SensorId)
            .ToListAsync();

        Console.WriteLine("\n  " + "ID Sensor".PadRight(25) + "Zona".PadRight(10) + "Tipos".PadRight(25) + "Leituras".PadLeft(10) + " " + "Ultima leitura".PadLeft(20));
        Console.WriteLine("  " + new string('-', 93));
        foreach (var s in sensors)
            Console.WriteLine($"  {s.SensorId,-25} {s.Zone,-10} {s.Types,-25} {s.Count,10} {s.LastReading:yyyy-MM-dd HH:mm:ss}");

        Console.WriteLine($"\n  Total: {sensors.Count} sensor(es)");
    }
    finally { dbLock.Release(); }
}

// ── 2: Consultar leituras ─────────────────────────────────────────────────────
async Task CmdQueryReadingsAsync()
{
    Console.Write("Sensor ID (vazio=todos): ");
    string? sid = Console.ReadLine()?.Trim();

    Console.Write("Tipo (TEMP/HUM/PM25/NO2/ACOUSTIC, vazio=todos): ");
    string? type = Console.ReadLine()?.Trim().ToUpper();

    Console.Write("Data início (yyyy-MM-dd HH:mm, vazio=últimas 24h): ");
    string? startStr = Console.ReadLine()?.Trim();

    Console.Write("Data fim (vazio=agora): ");
    string? endStr = Console.ReadLine()?.Trim();

    DateTime start = string.IsNullOrEmpty(startStr)
        ? DateTime.UtcNow.AddDays(-1)
        : DateTime.Parse(startStr).ToUniversalTime();
    DateTime end = string.IsNullOrEmpty(endStr)
        ? DateTime.UtcNow
        : DateTime.Parse(endStr).ToUniversalTime();

    Console.Write("Máx. resultados (vazio=50): ");
    string? limitStr = Console.ReadLine()?.Trim();
    int limit = string.IsNullOrEmpty(limitStr) ? 50 : int.Parse(limitStr);

    await dbLock.WaitAsync();
    try
    {
        var query = db.SensorReadings
            .Where(r => r.Timestamp >= start && r.Timestamp <= end);

        if (!string.IsNullOrEmpty(sid))
            query = query.Where(r => r.SensorId == sid);
        if (!string.IsNullOrEmpty(type))
            query = query.Where(r => r.Type == type);

        var results = await query
            .OrderByDescending(r => r.Timestamp)
            .Take(limit)
            .ToListAsync();

        Console.WriteLine("\n  " + "Timestamp".PadRight(22) + "Sensor".PadRight(25) + "Tipo".PadRight(10) + "Valor".PadLeft(10) + " " + "Unid".PadRight(8) + "Qual".PadRight(6) + "Src");
        Console.WriteLine("  " + new string('-', 90));
        foreach (var r in results)
            Console.WriteLine($"  {r.Timestamp:yyyy-MM-dd HH:mm:ss}  {r.SensorId,-25} {r.Type,-10} {r.Value,10:F2} {r.Unit,-8} {r.Quality,-6} {r.Source,-4}");

        Console.WriteLine($"\n  {results.Count} resultado(s).");
    }
    finally { dbLock.Release(); }
}

// ── 3: Estatísticas locais ────────────────────────────────────────────────────
async Task CmdStatisticsLocalAsync()
{
    Console.Write("Sensor ID (vazio=todos): ");
    string? sid = Console.ReadLine()?.Trim();

    Console.Write("Tipo (vazio=todos): ");
    string? type = Console.ReadLine()?.Trim().ToUpper();

    await dbLock.WaitAsync();
    try
    {
        var query = db.SensorReadings.AsQueryable();
        if (!string.IsNullOrEmpty(sid))   query = query.Where(r => r.SensorId == sid);
        if (!string.IsNullOrEmpty(type))  query = query.Where(r => r.Type == type);

        var groups = await query
            .GroupBy(r => new { r.SensorId, r.Type })
            .Select(g => new
            {
                g.Key.SensorId,
                g.Key.Type,
                Count   = g.Count(),
                Avg     = g.Average(r => r.Value),
                Min     = g.Min(r => r.Value),
                Max     = g.Max(r => r.Value),
                Outliers = g.Count(r => r.IsOutlier)
            })
            .OrderBy(g => g.SensorId).ThenBy(g => g.Type)
            .ToListAsync();

        Console.WriteLine("\n  " + "Sensor".PadRight(25) + "Tipo".PadRight(10) + "N".PadLeft(6) + "Avg".PadLeft(9) + "Min".PadLeft(9) + "Max".PadLeft(9) + "Outl".PadLeft(6));
        Console.WriteLine("  " + new string('-', 76));
        foreach (var g in groups)
            Console.WriteLine($"  {g.SensorId,-25} {g.Type,-10} {g.Count,6} {g.Avg,8:F2} {g.Min,8:F2} {g.Max,8:F2} {g.Outliers,5}");
    }
    finally { dbLock.Release(); }
}

// ── 4: Análise via RPC ────────────────────────────────────────────────────────
async Task CmdRequestAnalysisRpcAsync()
{
    Console.Write("Sensor ID: ");
    string? sid = Console.ReadLine()?.Trim();
    Console.Write("Tipo (TEMP/HUM/PM25/...): ");
    string? type = Console.ReadLine()?.Trim().ToUpper();
    Console.Write("Data início (yyyy-MM-dd, vazio=7 dias atrás): ");
    string? startStr = Console.ReadLine()?.Trim();
    Console.Write("Data fim (vazio=agora): ");
    string? endStr = Console.ReadLine()?.Trim();

    DateTime start = string.IsNullOrEmpty(startStr)
        ? DateTime.UtcNow.AddDays(-7) : DateTime.Parse(startStr).ToUniversalTime();
    DateTime end = string.IsNullOrEmpty(endStr)
        ? DateTime.UtcNow : DateTime.Parse(endStr).ToUniversalTime();

    // Primeiro calcular localmente os valores para enriquecer o RPC
    await dbLock.WaitAsync();
    List<SensorReadingEntity> readings;
    try
    {
        var query = db.SensorReadings
            .Where(r => r.Timestamp >= start && r.Timestamp <= end);
        if (!string.IsNullOrEmpty(sid))  query = query.Where(r => r.SensorId == sid);
        if (!string.IsNullOrEmpty(type)) query = query.Where(r => r.Type == type);
        readings = await query.OrderBy(r => r.Timestamp).ToListAsync();
    }
    finally { dbLock.Release(); }

    Console.WriteLine($"\n[INFO] {readings.Count} leituras locais. A invocar RPC...");

    // Calcular stats locais (para exibição imediata)
    if (readings.Count > 0)
    {
        double avg = readings.Average(r => r.Value);
        double mn  = readings.Min(r => r.Value);
        double mx  = readings.Max(r => r.Value);
        Console.WriteLine($"  [LOCAL] n={readings.Count} avg={avg:F2} min={mn:F2} max={mx:F2}");
    }

    // Chamada RPC
    var resp = await rpcAnalysis.AnalyzeReadingsAsync(
        sid ?? "*", type ?? "*", start, end);

    if (resp != null && resp.Status == "OK")
    {
        string resultJson = JsonSerializer.Serialize(new {
            sensorId = resp.SensorId, dataType = resp.DataType,
            count = resp.Count, average = resp.Average, min = resp.Minimum,
            max = resp.Maximum, stdDev = resp.StdDev, median = resp.Median,
            trend = resp.Trend, period = new { start, end }
        });

        await SaveAnalysisResultAsync(sid ?? "*", type ?? "*", "STATS", resultJson, start, end);
        Console.WriteLine($"\n  [RPC] Análise concluída e guardada na BD.");
        Console.WriteLine($"  Tendência: {resp.Trend}  |  Status: {resp.Status}");
    }
    else if (resp == null)
    {
        Console.WriteLine("[WARN] AnalysisService não disponível – resultado guardado apenas localmente.");
    }
}

// ── 5: Deteção de padrões ─────────────────────────────────────────────────────
async Task CmdDetectPatternsRpcAsync()
{
    Console.Write("Tipo de dado (PM25/NO2/TEMP/...): ");
    string? type = Console.ReadLine()?.Trim().ToUpper();
    Console.Write("Horas para trás (vazio=24): ");
    string? hoursStr = Console.ReadLine()?.Trim();
    int hours = string.IsNullOrEmpty(hoursStr) ? 24 : int.Parse(hoursStr);

    DateTime start = DateTime.UtcNow.AddHours(-hours);

    await dbLock.WaitAsync();
    List<SensorReadingEntity> readings;
    try
    {
        var query = db.SensorReadings.Where(r => r.Timestamp >= start);
        if (!string.IsNullOrEmpty(type)) query = query.Where(r => r.Type == type);
        readings = await query.OrderBy(r => r.Timestamp).ToListAsync();
    }
    finally { dbLock.Release(); }

    if (readings.Count == 0)
    {
        Console.WriteLine("[INFO] Sem leituras nesse período.");
        return;
    }

    Console.WriteLine($"[INFO] {readings.Count} leituras. A invocar RPC...");

    var points = readings.Select(r =>
        (r.Timestamp.ToString("o"), r.Value, r.SensorId)).ToList();

    var resp = await rpcAnalysis.DetectPatternsAsync(type ?? "*", points);

    if (resp != null && resp.Status == "OK")
    {
        Console.WriteLine($"\n  Resumo: {resp.Summary}");
        Console.WriteLine($"  Padrões detetados: {resp.Patterns.Count}");
        foreach (var p in resp.Patterns)
            Console.WriteLine($"    [{p.PatternType}] severity={p.Severity:F2} – {p.Description}");

        string json = JsonSerializer.Serialize(new {
            summary = resp.Summary,
            patterns = resp.Patterns.Select(p => new {
                p.PatternType, p.Description, p.Severity, p.StartTime, p.EndTime
            })
        });
        await SaveAnalysisResultAsync("*", type ?? "*", "PATTERNS", json, start, DateTime.UtcNow);
    }
}

// ── 6: Previsão de risco ──────────────────────────────────────────────────────
async Task CmdPredictRiskRpcAsync()
{
    Console.Write("Zona (ex: zona1, vazio=todas): ");
    string? zone = Console.ReadLine()?.Trim();

    // Obter leituras mais recentes de cada sensor/tipo
    await dbLock.WaitAsync();
    List<(string sid, string dtype, double val, string unit)> latest;
    try
    {
        var query = db.SensorReadings.AsQueryable();
        if (!string.IsNullOrEmpty(zone)) query = query.Where(r => r.Zone == zone);

        latest = await query
            .GroupBy(r => new { r.SensorId, r.Type })
            .Select(g => new {
                g.Key.SensorId,
                g.Key.Type,
                Value = g.OrderByDescending(r => r.Timestamp).First().Value,
                Unit  = g.OrderByDescending(r => r.Timestamp).First().Unit
            })
            .Select(x => ValueTuple.Create(x.SensorId, x.Type, x.Value, x.Unit))
            .ToListAsync();
    }
    finally { dbLock.Release(); }

    if (latest.Count == 0)
    {
        Console.WriteLine("[INFO] Sem dados para calcular risco.");
        return;
    }

    Console.WriteLine($"[INFO] {latest.Count} leituras recentes. A invocar RPC...");

    var resp = await rpcAnalysis.PredictHealthRiskAsync(zone ?? "geral", latest);

    if (resp != null && resp.Status == "OK")
    {
        Console.WriteLine($"\n  ╔══════════════════════════════╗");
        Console.WriteLine($"  ║  RISCO: {resp.RiskLevel,-10} Score: {resp.RiskScore:F1}/100 ║");
        Console.WriteLine($"  ╚══════════════════════════════╝");
        Console.WriteLine($"  {resp.Summary}");
        Console.WriteLine("\n  Recomendações:");
        foreach (var r in resp.Recommendations)
            Console.WriteLine($"    • {r}");

        string json = JsonSerializer.Serialize(new {
            zone, resp.RiskLevel, resp.RiskScore, resp.Summary,
            recommendations = resp.Recommendations
        });
        await SaveAnalysisResultAsync("*", "*", "RISK", json, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, zone ?? "geral");
    }
}

// ── 7: Listar resultados de análises ─────────────────────────────────────────
async Task CmdListAnalysisResultsAsync()
{
    await dbLock.WaitAsync();
    try
    {
        var results = await db.AnalysisResults
            .OrderByDescending(a => a.CreatedAt)
            .Take(20)
            .ToListAsync();

        Console.WriteLine("\n  " + "ID".PadLeft(5) + " " + "Tipo".PadRight(10) + "Sensor".PadRight(25) + "DataType".PadRight(10) + "Zona".PadRight(10) + "Criado".PadRight(22));
        Console.WriteLine("  " + new string('─', 90));
        foreach (var r in results)
            Console.WriteLine($"  {r.Id,5} {r.AnalysisType,10} {r.SensorId,25} {r.DataType,10} {r.Zone,10} {r.CreatedAt:yyyy-MM-dd HH:mm:ss}");

        Console.WriteLine($"\n  {results.Count} resultado(s).");

        Console.Write("\nVer detalhe de resultado (ID, vazio=ignorar): ");
        string? idStr = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(idStr) && int.TryParse(idStr, out int id))
        {
            var detail = results.FirstOrDefault(r => r.Id == id);
            if (detail != null)
            {
                Console.WriteLine($"\n  ── Resultado #{id} ───────────────────────");
                // Pretty-print JSON
                var parsed = JsonDocument.Parse(detail.ResultJson);
                Console.WriteLine(JsonSerializer.Serialize(parsed,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
        }
    }
    finally { dbLock.Release(); }
}

// ── 8: Exportar CSV ───────────────────────────────────────────────────────────
async Task CmdExportCsvAsync()
{
    Console.Write("Ficheiro de destino (vazio=export.csv): ");
    string? fileName = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(fileName)) fileName = "export.csv";

    Console.Write("Sensor ID (vazio=todos): ");
    string? sid = Console.ReadLine()?.Trim();
    Console.Write("Tipo (vazio=todos): ");
    string? type = Console.ReadLine()?.Trim().ToUpper();

    await dbLock.WaitAsync();
    try
    {
        var query = db.SensorReadings.AsQueryable();
        if (!string.IsNullOrEmpty(sid))  query = query.Where(r => r.SensorId == sid);
        if (!string.IsNullOrEmpty(type)) query = query.Where(r => r.Type == type);
        var data = await query.OrderBy(r => r.Timestamp).ToListAsync();

        using var writer = new StreamWriter(fileName, false, Encoding.UTF8);
        await writer.WriteLineAsync("timestamp,sensorId,zone,type,value,unit,quality,isOutlier,source");
        foreach (var r in data)
            await writer.WriteLineAsync(
                $"{r.Timestamp:o},{r.SensorId},{r.Zone},{r.Type},{r.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)},{r.Unit},{r.Quality},{r.IsOutlier},{r.Source}");

        Console.WriteLine($"[INFO] {data.Count} registos exportados para '{fileName}'.");
    }
    finally { dbLock.Release(); }
}

// ── Auxiliares ────────────────────────────────────────────────────────────────
async Task SaveAnalysisResultAsync(string sensorId, string dataType, string analysisType,
    string resultJson, DateTime start, DateTime end, string zone = "")
{
    await dbLock.WaitAsync();
    try
    {
        db.AnalysisResults.Add(new AnalysisResultEntity
        {
            SensorId     = sensorId,
            DataType     = dataType,
            AnalysisType = analysisType,
            ResultJson   = resultJson,
            StartPeriod  = start,
            EndPeriod    = end,
            CreatedAt    = DateTime.UtcNow,
            Zone         = zone
        });
        await db.SaveChangesAsync();
    }
    finally { dbLock.Release(); }
}

string ExtractZone(string sensorId)
{
    // Convenção: SENSOR_ZONA1_001 → zona1
    var parts = sensorId.ToLower().Split('_');
    return parts.Length >= 2 ? parts[1] : "desconhecida";
}

string GetDefaultUnit(string type) => type.ToUpper() switch
{
    "TEMP"     => "°C",
    "HUM"      => "%",
    "PM25"     => "µg/m³",
    "NO2"      => "µg/m³",
    "ACOUSTIC" => "dB",
    "CO"       => "ppm",
    "O3"       => "µg/m³",
    _          => ""
};
