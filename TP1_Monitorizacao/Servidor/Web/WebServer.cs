using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Servidor.Data;
using Servidor.Services;

namespace Servidor.Web
{
    /// <summary>
    /// Servidor HTTP simples (porta 8080) que serve o dashboard web
    /// e expõe uma API JSON para o frontend consultar/acionar análises.
    /// </summary>
    public class WebServer
    {
        private readonly HttpListener _listener;
        private readonly ServerDbContext _db;
        private readonly SemaphoreSlim _dbLock;
        private readonly RpcAnalysisClient _rpc;
        private readonly int _port;

        public WebServer(ServerDbContext db, SemaphoreSlim dbLock, RpcAnalysisClient rpc, int port = 8080)
        {
            _db     = db;
            _dbLock = dbLock;
            _rpc    = rpc;
            _port   = port;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
        }

        public void Start()
        {
            _listener.Start();
            Console.WriteLine($"[WEB] Dashboard disponivel em http://localhost:{_port}/");
            Task.Run(ListenLoop);
        }

        private async Task ListenLoop()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(ctx));
                }
                catch { /* listener fechado */ }
            }
        }

        private async Task HandleRequest(HttpListenerContext ctx)
        {
            var req  = ctx.Request;
            var resp = ctx.Response;
            resp.Headers.Add("Access-Control-Allow-Origin", "*");

            try
            {
                string path = req.Url?.AbsolutePath ?? "/";

                if (path == "/" || path == "/index.html")
                {
                    await ServeHtml(resp);
                }
                else if (path == "/api/sensors" && req.HttpMethod == "GET")
                {
                    await ServeJson(resp, await GetSensorsAsync());
                }
                else if (path == "/api/readings" && req.HttpMethod == "GET")
                {
                    string? sid   = req.QueryString["sensorId"];
                    string? type  = req.QueryString["type"];
                    int hours     = int.TryParse(req.QueryString["hours"], out int h) ? h : 1;
                    await ServeJson(resp, await GetReadingsAsync(sid, type, hours));
                }
                else if (path == "/api/stats" && req.HttpMethod == "GET")
                {
                    await ServeJson(resp, await GetStatsAsync());
                }
                else if (path == "/api/analyses" && req.HttpMethod == "GET")
                {
                    await ServeJson(resp, await GetAnalysesAsync());
                }
                else if (path == "/api/analyze" && req.HttpMethod == "POST")
                {
                    using var sr = new StreamReader(req.InputStream);
                    string body  = await sr.ReadToEndAsync();
                    await ServeJson(resp, await TriggerAnalysisAsync(body));
                }
                else if (path == "/api/risk" && req.HttpMethod == "POST")
                {
                    using var sr = new StreamReader(req.InputStream);
                    string body  = await sr.ReadToEndAsync();
                    await ServeJson(resp, await TriggerRiskAsync(body));
                }
                else
                {
                    resp.StatusCode = 404;
                    byte[] buf = Encoding.UTF8.GetBytes("{\"error\":\"not found\"}");
                    resp.ContentType = "application/json";
                    resp.ContentLength64 = buf.Length;
                    await resp.OutputStream.WriteAsync(buf);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WEB ERROR] {ex.Message}");
                resp.StatusCode = 500;
            }
            finally
            {
                resp.Close();
            }
        }

        // ── API handlers ──────────────────────────────────────────────────────

        private async Task<object> GetSensorsAsync()
        {
            await _dbLock.WaitAsync();
            try
            {
                return await _db.SensorReadings
                    .GroupBy(r => new { r.SensorId, r.Zone })
                    .Select(g => new
                    {
                        sensorId    = g.Key.SensorId,
                        zone        = g.Key.Zone,
                        count       = g.Count(),
                        lastReading = g.Max(r => r.Timestamp),
                        types       = g.Select(r => r.Type).Distinct().ToList()
                    })
                    .OrderBy(s => s.sensorId)
                    .ToListAsync();
            }
            finally { _dbLock.Release(); }
        }

        private async Task<object> GetReadingsAsync(string? sensorId, string? type, int hours)
        {
            await _dbLock.WaitAsync();
            try
            {
                var since = DateTime.UtcNow.AddHours(-hours);
                var q = _db.SensorReadings.Where(r => r.Timestamp >= since);
                if (!string.IsNullOrEmpty(sensorId)) q = q.Where(r => r.SensorId == sensorId);
                if (!string.IsNullOrEmpty(type))     q = q.Where(r => r.Type == type);

                return await q
                    .OrderByDescending(r => r.Timestamp)
                    .Take(200)
                    .Select(r => new
                    {
                        timestamp = r.Timestamp.ToString("o"),
                        sensorId  = r.SensorId,
                        zone      = r.Zone,
                        type      = r.Type,
                        value     = r.Value,
                        unit      = r.Unit,
                        quality   = r.Quality,
                        isOutlier = r.IsOutlier,
                        source    = r.Source
                    })
                    .ToListAsync();
            }
            finally { _dbLock.Release(); }
        }

        private async Task<object> GetStatsAsync()
        {
            await _dbLock.WaitAsync();
            try
            {
                return await _db.SensorReadings
                    .GroupBy(r => new { r.SensorId, r.Type })
                    .Select(g => new
                    {
                        sensorId = g.Key.SensorId,
                        type     = g.Key.Type,
                        count    = g.Count(),
                        avg      = g.Average(r => r.Value),
                        min      = g.Min(r => r.Value),
                        max      = g.Max(r => r.Value),
                        outliers = g.Count(r => r.IsOutlier),
                        last     = g.Max(r => r.Timestamp)
                    })
                    .OrderBy(g => g.sensorId)
                    .ToListAsync();
            }
            finally { _dbLock.Release(); }
        }

        private async Task<object> GetAnalysesAsync()
        {
            await _dbLock.WaitAsync();
            try
            {
                var items = await _db.AnalysisResults
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(10)
                    .ToListAsync();

                return items.Select(a => new
                {
                    id           = a.Id,
                    analysisType = a.AnalysisType,
                    sensorId     = a.SensorId,
                    dataType     = a.DataType,
                    zone         = a.Zone,
                    createdAt    = a.CreatedAt.ToString("o"),
                    result       = JsonDocument.Parse(a.ResultJson).RootElement
                }).ToList();
            }
            finally { _dbLock.Release(); }
        }

        private async Task<object> TriggerAnalysisAsync(string body)
        {
            try
            {
                using var doc   = JsonDocument.Parse(body);
                string sensorId = doc.RootElement.TryGetProperty("sensorId", out var s) ? s.GetString()! : "*";
                string dataType = doc.RootElement.TryGetProperty("dataType", out var t) ? t.GetString()! : "*";
                int    hours    = doc.RootElement.TryGetProperty("hours", out var hh)   ? hh.GetInt32()  : 24;

                var end   = DateTime.UtcNow;
                var start = end.AddHours(-hours);

                // Calcular stats locais
                await _dbLock.WaitAsync();
                List<double> vals;
                try
                {
                    var q = _db.SensorReadings.Where(r => r.Timestamp >= start && r.Timestamp <= end);
                    if (sensorId != "*") q = q.Where(r => r.SensorId == sensorId);
                    if (dataType != "*") q = q.Where(r => r.Type == dataType);
                    vals = await q.Select(r => r.Value).ToListAsync();
                }
                finally { _dbLock.Release(); }

                if (vals.Count == 0)
                    return new { status = "error", message = "Sem dados no periodo especificado." };

                double avg = vals.Average();
                double mn  = vals.Min();
                double mx  = vals.Max();
                double std = Math.Sqrt(vals.Average(v => Math.Pow(v - avg, 2)));

                // Chamar RPC
                var rpcResp = await _rpc.AnalyzeReadingsAsync(sensorId, dataType, start, end);

                var result = new
                {
                    status   = "ok",
                    sensorId, dataType,
                    period   = new { start = start.ToString("o"), end = end.ToString("o") },
                    local    = new { count = vals.Count, avg, min = mn, max = mx, stdDev = std },
                    rpc      = rpcResp != null ? new { rpcResp.Trend, rpcResp.Status } : null
                };

                // Guardar na BD
                string json = JsonSerializer.Serialize(result);
                await _dbLock.WaitAsync();
                try
                {
                    _db.AnalysisResults.Add(new AnalysisResultEntity
                    {
                        SensorId     = sensorId,
                        DataType     = dataType,
                        AnalysisType = "STATS",
                        ResultJson   = json,
                        StartPeriod  = start,
                        EndPeriod    = end,
                        CreatedAt    = DateTime.UtcNow
                    });
                    await _db.SaveChangesAsync();
                }
                finally { _dbLock.Release(); }

                return result;
            }
            catch (Exception ex)
            {
                return new { status = "error", message = ex.Message };
            }
        }

        private async Task<object> TriggerRiskAsync(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                string zone   = doc.RootElement.TryGetProperty("zone", out var z) ? z.GetString()! : "geral";

                // Leituras mais recentes por sensor/tipo
                await _dbLock.WaitAsync();
                List<(string sid, string dtype, double val, string unit)> latest;
                try
                {
                    var q = _db.SensorReadings.AsQueryable();
                    if (zone != "geral") q = q.Where(r => r.Zone == zone);

                    latest = (await q
                        .GroupBy(r => new { r.SensorId, r.Type })
                        .Select(g => new {
                            g.Key.SensorId, g.Key.Type,
                            Value = g.OrderByDescending(r => r.Timestamp).First().Value,
                            Unit  = g.OrderByDescending(r => r.Timestamp).First().Unit
                        })
                        .ToListAsync())
                        .Select(x => (x.SensorId, x.Type, x.Value, x.Unit))
                        .ToList();
                }
                finally { _dbLock.Release(); }

                if (latest.Count == 0)
                    return new { status = "error", message = "Sem dados para calcular risco." };

                var rpcResp = await _rpc.PredictHealthRiskAsync(zone, latest);

                if (rpcResp == null)
                    return new { status = "error", message = "AnalysisService nao disponivel." };

                var result = new
                {
                    status          = "ok",
                    zone,
                    riskLevel       = rpcResp.RiskLevel,
                    riskScore       = rpcResp.RiskScore,
                    summary         = rpcResp.Summary,
                    recommendations = rpcResp.Recommendations.ToList()
                };

                string json = JsonSerializer.Serialize(result);
                await _dbLock.WaitAsync();
                try
                {
                    _db.AnalysisResults.Add(new AnalysisResultEntity
                    {
                        SensorId     = "*",
                        DataType     = "*",
                        AnalysisType = "RISK",
                        ResultJson   = json,
                        StartPeriod  = DateTime.UtcNow.AddHours(-1),
                        EndPeriod    = DateTime.UtcNow,
                        CreatedAt    = DateTime.UtcNow,
                        Zone         = zone
                    });
                    await _db.SaveChangesAsync();
                }
                finally { _dbLock.Release(); }

                return result;
            }
            catch (Exception ex)
            {
                return new { status = "error", message = ex.Message };
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private async Task ServeJson(HttpListenerResponse resp, object data)
        {
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = false });
            byte[] buf  = Encoding.UTF8.GetBytes(json);
            resp.ContentType     = "application/json; charset=utf-8";
            resp.ContentLength64 = buf.Length;
            await resp.OutputStream.WriteAsync(buf);
        }

        private async Task ServeHtml(HttpListenerResponse resp)
        {
            byte[] buf = Encoding.UTF8.GetBytes(DashboardHtml());
            resp.ContentType     = "text/html; charset=utf-8";
            resp.ContentLength64 = buf.Length;
            await resp.OutputStream.WriteAsync(buf);
        }

        // ── Dashboard HTML ────────────────────────────────────────────────────
        private static string DashboardHtml() => @"<!DOCTYPE html>
<html lang='pt'>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<title>One Health - Monitor Urbano</title>
<script src='https://cdnjs.cloudflare.com/ajax/libs/Chart.js/4.4.1/chart.umd.min.js'></script>
<style>
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body { font-family: 'Segoe UI', sans-serif; background: #0f172a; color: #e2e8f0; min-height: 100vh; }
  header { background: linear-gradient(135deg, #1e3a5f, #0ea5e9); padding: 18px 32px; display: flex; align-items: center; gap: 16px; }
  header h1 { font-size: 1.4rem; font-weight: 700; color: #fff; }
  header span { font-size: 0.85rem; color: #bae6fd; }
  .badge { background: #10b981; color: #fff; border-radius: 12px; padding: 2px 10px; font-size: 0.75rem; margin-left: 8px; }
  .badge.warn { background: #f59e0b; }
  .badge.danger { background: #ef4444; }
  main { padding: 24px 32px; display: grid; gap: 20px; }
  .grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }
  .grid3 { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 20px; }
  .card { background: #1e293b; border-radius: 12px; padding: 20px; border: 1px solid #334155; }
  .card h2 { font-size: 0.9rem; font-weight: 600; color: #94a3b8; text-transform: uppercase; letter-spacing: 0.05em; margin-bottom: 14px; }
  .stat-value { font-size: 2rem; font-weight: 700; color: #38bdf8; }
  .stat-label { font-size: 0.8rem; color: #64748b; margin-top: 2px; }
  table { width: 100%; border-collapse: collapse; font-size: 0.82rem; }
  th { text-align: left; padding: 8px 10px; color: #64748b; border-bottom: 1px solid #334155; font-weight: 600; }
  td { padding: 7px 10px; border-bottom: 1px solid #1e293b; }
  tr:hover td { background: #263348; }
  .quality-GOOD { color: #10b981; font-weight: 600; }
  .quality-FAIR { color: #f59e0b; font-weight: 600; }
  .quality-POOR { color: #ef4444; font-weight: 600; }
  .risk-LOW      { color: #10b981; font-weight: 700; font-size: 1.1rem; }
  .risk-MODERATE { color: #f59e0b; font-weight: 700; font-size: 1.1rem; }
  .risk-HIGH     { color: #f97316; font-weight: 700; font-size: 1.1rem; }
  .risk-CRITICAL { color: #ef4444; font-weight: 700; font-size: 1.1rem; }
  .controls { display: flex; gap: 10px; flex-wrap: wrap; align-items: flex-end; }
  select, input { background: #0f172a; border: 1px solid #334155; color: #e2e8f0; border-radius: 8px; padding: 7px 12px; font-size: 0.85rem; }
  button { background: #0ea5e9; color: #fff; border: none; border-radius: 8px; padding: 8px 18px; cursor: pointer; font-size: 0.85rem; font-weight: 600; transition: background 0.2s; }
  button:hover { background: #0284c7; }
  button.green { background: #10b981; }
  button.green:hover { background: #059669; }
  button.orange { background: #f59e0b; }
  button.orange:hover { background: #d97706; }
  .refresh-info { font-size: 0.75rem; color: #475569; text-align: right; margin-top: 4px; }
  .chart-wrap { position: relative; height: 220px; }
  .result-box { background: #0f172a; border-radius: 8px; padding: 14px; margin-top: 10px; font-size: 0.82rem; line-height: 1.6; border-left: 3px solid #0ea5e9; display: none; }
  .rec-list { margin-top: 8px; padding-left: 16px; }
  .rec-list li { margin-bottom: 4px; }
  #status-dot { width: 10px; height: 10px; border-radius: 50%; background: #10b981; display: inline-block; margin-right: 6px; animation: pulse 2s infinite; }
  @keyframes pulse { 0%,100%{opacity:1} 50%{opacity:.4} }
</style>
</head>
<body>
<header>
  <div>
    <h1>&#127758; One Health &mdash; Monitor Ambiental Urbano</h1>
    <span><span id='status-dot'></span>Em tempo real &bull; Atualiza a cada 5s &bull; <span id='last-update'>--</span></span>
  </div>
</header>
<main>

  <!-- Contadores -->
  <div class='grid3'>
    <div class='card'>
      <h2>Sensores Ativos</h2>
      <div class='stat-value' id='cnt-sensors'>--</div>
      <div class='stat-label'>sensores registados</div>
    </div>
    <div class='card'>
      <h2>Leituras (ultima hora)</h2>
      <div class='stat-value' id='cnt-readings'>--</div>
      <div class='stat-label'>registos na BD</div>
    </div>
    <div class='card'>
      <h2>Analises Realizadas</h2>
      <div class='stat-value' id='cnt-analyses'>--</div>
      <div class='stat-label'>via RPC</div>
    </div>
  </div>

  <!-- Grafico + Sensores -->
  <div class='grid2'>
    <div class='card'>
      <h2>Leituras Recentes &mdash; Grafico</h2>
      <div class='controls' style='margin-bottom:12px'>
        <div>
          <label style='font-size:0.78rem;color:#64748b'>Sensor</label>
          <select id='chart-sensor'><option value=''>Todos</option></select>
        </div>
        <div>
          <label style='font-size:0.78rem;color:#64748b'>Tipo</label>
          <select id='chart-type'>
            <option value='TEMP'>TEMP</option>
            <option value='HUM'>HUM</option>
            <option value='PM25'>PM25</option>
            <option value='NO2'>NO2</option>
            <option value='ACOUSTIC'>ACOUSTIC</option>
          </select>
        </div>
      </div>
      <div class='chart-wrap'><canvas id='chart-readings'></canvas></div>
    </div>
    <div class='card'>
      <h2>Sensores Registados</h2>
      <table id='tbl-sensors'>
        <thead><tr><th>ID Sensor</th><th>Zona</th><th>Tipos</th><th>Leituras</th><th>Ultima</th></tr></thead>
        <tbody></tbody>
      </table>
    </div>
  </div>

  <!-- Tabela leituras recentes -->
  <div class='card'>
    <h2>Ultimas Leituras</h2>
    <table id='tbl-readings'>
      <thead><tr><th>Timestamp</th><th>Sensor</th><th>Tipo</th><th>Valor</th><th>Unidade</th><th>Qualidade</th><th>Outlier</th></tr></thead>
      <tbody></tbody>
    </table>
    <div class='refresh-info' id='readings-count'></div>
  </div>

  <!-- Estatisticas -->
  <div class='card'>
    <h2>Estatisticas por Sensor / Tipo</h2>
    <table id='tbl-stats'>
      <thead><tr><th>Sensor</th><th>Tipo</th><th>N</th><th>Media</th><th>Min</th><th>Max</th><th>Outliers</th><th>Ultima Leitura</th></tr></thead>
      <tbody></tbody>
    </table>
  </div>

  <!-- Analise RPC -->
  <div class='grid2'>
    <div class='card'>
      <h2>Analise Estatistica via RPC</h2>
      <div class='controls'>
        <div>
          <label style='font-size:0.78rem;color:#64748b'>Sensor ID</label>
          <input id='an-sensor' placeholder='* = todos' value='*'>
        </div>
        <div>
          <label style='font-size:0.78rem;color:#64748b'>Tipo</label>
          <select id='an-type'>
            <option value='*'>Todos</option>
            <option value='TEMP'>TEMP</option>
            <option value='HUM'>HUM</option>
            <option value='PM25'>PM25</option>
            <option value='NO2'>NO2</option>
            <option value='ACOUSTIC'>ACOUSTIC</option>
          </select>
        </div>
        <div>
          <label style='font-size:0.78rem;color:#64748b'>Ultimas (horas)</label>
          <input id='an-hours' type='number' value='24' style='width:70px'>
        </div>
        <button onclick='runAnalysis()'>&#128202; Analisar</button>
      </div>
      <div class='result-box' id='an-result'></div>
    </div>
    <div class='card'>
      <h2>Previsao de Risco de Saude via RPC</h2>
      <div class='controls'>
        <div>
          <label style='font-size:0.78rem;color:#64748b'>Zona</label>
          <input id='risk-zone' placeholder='zona1' value='zona1'>
        </div>
        <button class='orange' onclick='runRisk()'>&#9888; Calcular Risco</button>
      </div>
      <div class='result-box' id='risk-result'></div>
    </div>
  </div>

  <!-- Historico de analises -->
  <div class='card'>
    <h2>Historico de Analises RPC</h2>
    <table id='tbl-analyses'>
      <thead><tr><th>Data</th><th>Tipo</th><th>Sensor</th><th>Dado</th><th>Zona</th><th>Resultado</th></tr></thead>
      <tbody></tbody>
    </table>
  </div>

</main>

<script>
let chart = null;
let allSensors = [];

// ── Inicializar grafico ───────────────────────────────────────────────────────
function initChart() {
  const ctx = document.getElementById('chart-readings').getContext('2d');
  chart = new Chart(ctx, {
    type: 'line',
    data: { labels: [], datasets: [{ label: 'Valor', data: [], borderColor: '#0ea5e9', backgroundColor: 'rgba(14,165,233,0.1)', tension: 0.3, pointRadius: 2, fill: true }] },
    options: {
      responsive: true, maintainAspectRatio: false,
      plugins: { legend: { display: false } },
      scales: {
        x: { ticks: { color: '#64748b', maxTicksLimit: 8, font: { size: 10 } }, grid: { color: '#1e293b' } },
        y: { ticks: { color: '#64748b' }, grid: { color: '#334155' } }
      }
    }
  });
}

// ── Fetch helper ──────────────────────────────────────────────────────────────
async function api(path, opts) {
  try {
    const r = await fetch(path, opts);
    return await r.json();
  } catch(e) { return null; }
}

// ── Atualizar tudo ────────────────────────────────────────────────────────────
async function refresh() {
  const [sensors, stats, analyses] = await Promise.all([
    api('/api/sensors'),
    api('/api/stats'),
    api('/api/analyses')
  ]);

  if (sensors) updateSensors(sensors);
  if (stats)   updateStats(stats);
  if (analyses) updateAnalyses(analyses);

  await updateReadings();
  await updateChart();

  document.getElementById('last-update').textContent = new Date().toLocaleTimeString('pt-PT');
}

function updateSensors(sensors) {
  allSensors = sensors;
  document.getElementById('cnt-sensors').textContent = sensors.length;

  // Atualizar dropdown do grafico
  const sel = document.getElementById('chart-sensor');
  const cur = sel.value;
  sel.innerHTML = '<option value=\"\">Todos</option>';
  sensors.forEach(s => {
    const o = document.createElement('option');
    o.value = s.sensorId; o.textContent = s.sensorId;
    if (s.sensorId === cur) o.selected = true;
    sel.appendChild(o);
  });

  const tbody = document.querySelector('#tbl-sensors tbody');
  tbody.innerHTML = sensors.map(s => `
    <tr>
      <td>${s.sensorId}</td>
      <td>${s.zone}</td>
      <td style='font-size:0.75rem;color:#94a3b8'>${(s.types||[]).join(', ')}</td>
      <td>${s.count}</td>
      <td style='font-size:0.75rem'>${fmtDate(s.lastReading)}</td>
    </tr>`).join('');
}

async function updateReadings() {
  const data = await api('/api/readings?hours=1');
  if (!data) return;
  document.getElementById('cnt-readings').textContent = data.length;
  const tbody = document.querySelector('#tbl-readings tbody');
  tbody.innerHTML = data.slice(0,50).map(r => `
    <tr>
      <td style='font-size:0.75rem'>${fmtDate(r.timestamp)}</td>
      <td style='font-size:0.75rem'>${r.sensorId}</td>
      <td><b>${r.type}</b></td>
      <td>${r.value.toFixed(2)}</td>
      <td style='color:#64748b'>${r.unit}</td>
      <td class='quality-${r.quality}'>${r.quality}</td>
      <td style='color:${r.isOutlier?\"#ef4444\":\"#10b981\"}'>${r.isOutlier?'&#9888; Sim':'OK'}</td>
    </tr>`).join('');
  document.getElementById('readings-count').textContent = `A mostrar ${Math.min(50,data.length)} de ${data.length} leituras na ultima hora`;
}

async function updateChart() {
  const sensor = document.getElementById('chart-sensor').value;
  const type   = document.getElementById('chart-type').value;
  let url = `/api/readings?hours=1&type=${type}`;
  if (sensor) url += `&sensorId=${sensor}`;
  const data = await api(url);
  if (!data || !chart) return;

  const sorted = data.slice().reverse();
  chart.data.labels = sorted.map(r => fmtDate(r.timestamp, true));
  chart.data.datasets[0].data = sorted.map(r => r.value);
  chart.data.datasets[0].label = type;
  chart.update('none');
}

function updateStats(stats) {
  const tbody = document.querySelector('#tbl-stats tbody');
  tbody.innerHTML = stats.map(s => `
    <tr>
      <td style='font-size:0.78rem'>${s.sensorId}</td>
      <td><b>${s.type}</b></td>
      <td>${s.count}</td>
      <td>${s.avg.toFixed(2)}</td>
      <td>${s.min.toFixed(2)}</td>
      <td>${s.max.toFixed(2)}</td>
      <td style='color:${s.outliers>0?\"#f59e0b\":\"#10b981\"}'>${s.outliers}</td>
      <td style='font-size:0.75rem'>${fmtDate(s.last)}</td>
    </tr>`).join('');
}

function updateAnalyses(analyses) {
  document.getElementById('cnt-analyses').textContent = analyses.length;
  const tbody = document.querySelector('#tbl-analyses tbody');
  tbody.innerHTML = analyses.map(a => {
    let summary = '';
    try {
      const r = a.result;
      if (a.analysisType === 'RISK') summary = `<span class='risk-${r.riskLevel}'>${r.riskLevel}</span> (${r.riskScore?.toFixed(1)})`;
      else if (a.analysisType === 'STATS') summary = `avg=${r.local?.avg?.toFixed(2)} n=${r.local?.count}`;
      else if (a.analysisType === 'PATTERNS') summary = r.summary?.slice(0,60)+'...';
    } catch(e) {}
    return `<tr>
      <td style='font-size:0.75rem'>${fmtDate(a.createdAt)}</td>
      <td><b>${a.analysisType}</b></td>
      <td style='font-size:0.75rem'>${a.sensorId}</td>
      <td>${a.dataType}</td>
      <td>${a.zone||'-'}</td>
      <td>${summary}</td>
    </tr>`;
  }).join('');
}

// ── Acoes RPC ─────────────────────────────────────────────────────────────────
async function runAnalysis() {
  const box = document.getElementById('an-result');
  box.style.display = 'block';
  box.innerHTML = 'A analisar via RPC...';
  const r = await api('/api/analyze', {
    method: 'POST',
    headers: {'Content-Type':'application/json'},
    body: JSON.stringify({
      sensorId: document.getElementById('an-sensor').value || '*',
      dataType: document.getElementById('an-type').value || '*',
      hours:    parseInt(document.getElementById('an-hours').value) || 24
    })
  });
  if (!r) { box.innerHTML = 'Erro ao contactar servidor.'; return; }
  if (r.status === 'error') { box.innerHTML = '<b style=\"color:#ef4444\">Erro:</b> ' + r.message; return; }
  box.innerHTML = `
    <b>Sensor:</b> ${r.sensorId} &nbsp;|&nbsp; <b>Tipo:</b> ${r.dataType}<br>
    <b>N:</b> ${r.local?.count} &nbsp;|&nbsp;
    <b>Media:</b> ${r.local?.avg?.toFixed(2)} &nbsp;|&nbsp;
    <b>Min:</b> ${r.local?.min?.toFixed(2)} &nbsp;|&nbsp;
    <b>Max:</b> ${r.local?.max?.toFixed(2)} &nbsp;|&nbsp;
    <b>StdDev:</b> ${r.local?.stdDev?.toFixed(2)}<br>
    <b>Tendencia RPC:</b> ${r.rpc?.Trend || 'N/D'} &nbsp;|&nbsp; <b>Status:</b> ${r.rpc?.Status || 'N/D'}
  `;
  await refresh();
}

async function runRisk() {
  const box = document.getElementById('risk-result');
  box.style.display = 'block';
  box.innerHTML = 'A calcular risco via RPC...';
  const r = await api('/api/risk', {
    method: 'POST',
    headers: {'Content-Type':'application/json'},
    body: JSON.stringify({ zone: document.getElementById('risk-zone').value || 'zona1' })
  });
  if (!r) { box.innerHTML = 'Erro ao contactar servidor.'; return; }
  if (r.status === 'error') { box.innerHTML = '<b style=\"color:#ef4444\">Erro:</b> ' + r.message; return; }
  box.innerHTML = `
    <b>Zona:</b> ${r.zone} &nbsp;|&nbsp;
    <b>Risco:</b> <span class='risk-${r.riskLevel}'>${r.riskLevel}</span> &nbsp;|&nbsp;
    <b>Score:</b> ${r.riskScore?.toFixed(1)}/100<br>
    <i>${r.summary}</i>
    <ul class='rec-list'>${(r.recommendations||[]).map(x=>'<li>'+x+'</li>').join('')}</ul>
  `;
  await refresh();
}

// ── Formatacao de datas ───────────────────────────────────────────────────────
function fmtDate(iso, short = false) {
  if (!iso) return '-';
  const d = new Date(iso);
  if (short) return d.toLocaleTimeString('pt-PT');
  return d.toLocaleDateString('pt-PT') + ' ' + d.toLocaleTimeString('pt-PT');
}

// ── Arranque ──────────────────────────────────────────────────────────────────
initChart();
refresh();
setInterval(refresh, 5000);

document.getElementById('chart-sensor').addEventListener('change', updateChart);
document.getElementById('chart-type').addEventListener('change', updateChart);
</script>
</body>
</html>";
    }
}
