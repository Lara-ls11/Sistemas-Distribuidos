/*
 * Gateway – TP2
 * ─────────────────────────────────────────────────────────────────────────────
 * • Subscrevê tópicos RabbitMQ (sensor.<zona>.#)
 * • Invoca PreprocessingService via RPC (gRPC) para normalizar cada leitura
 * • Agrega dados por janelas de 15 min e envia ao Servidor via TCP
 * • Mantém persistência local em SQLite (legado TP1)
 *
 * Argumentos: [zona] [rabbitmq_host] [preprocessing_host]
 */

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Gateway.Managers;
using Gateway.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

// ── Configuração ──────────────────────────────────────────────────────────────
string zone            = args.Length > 0 ? args[0] : "zona1";
string rabbitHost      = args.Length > 1 ? args[1] : "localhost";
string rpcHost         = args.Length > 2 ? args[2] : "localhost";
string serverHost      = "localhost";
int    serverPort      = 5002;
string exchangeName    = "sensors_exchange";
string gatewayId       = $"GW_{zone.ToUpper()}";

Console.WriteLine("╔══════════════════════════════════════╗");
Console.WriteLine("║     GATEWAY – Pub/Sub + RPC gRPC     ║");
Console.WriteLine("╚══════════════════════════════════════╝");
Console.WriteLine($"[INFO] Gateway ID : {gatewayId}");
Console.WriteLine($"[INFO] Zona       : {zone}");
Console.WriteLine($"[INFO] RabbitMQ   : {rabbitHost}");
Console.WriteLine($"[INFO] RPC Preproc: {rpcHost}:50051");
Console.WriteLine($"[INFO] Servidor   : {serverHost}:{serverPort}");
Console.WriteLine();

// ── Serviços internos (legado TP1) ────────────────────────────────────────────
var sensorManager      = new SensorManager();
var fileManager        = new FileManager();
var databaseManager    = new DatabaseManager();
var aggregationService = new DataAggregationService();
var serverForwarder    = new ServerForwarderService(serverHost, serverPort);

// ── Cliente RPC de pré-processamento ─────────────────────────────────────────
using var rpcPreprocessing = new RpcPreprocessingClient(rpcHost, 50051);

// ── Buffer de agregação: zone+sensorId+type → lista de valores ────────────────
var aggBuffer = new Dictionary<string, List<(double value, DateTime ts)>>();
var aggLock   = new SemaphoreSlim(1, 1);

// ── Ligação ao RabbitMQ ───────────────────────────────────────────────────────
IConnection? conn    = null;
IChannel?    channel = null;

while (true)
{
    try
    {
        var factory = new ConnectionFactory { HostName = rabbitHost };
        conn    = await factory.CreateConnectionAsync();
        channel = await conn.CreateChannelAsync();

        // Garantir que o exchange existe
        await channel.ExchangeDeclareAsync(
            exchange:   exchangeName,
            type:       ExchangeType.Topic,
            durable:    true,
            autoDelete: false);

        // Fila exclusiva para este gateway
        var qDeclare = await channel.QueueDeclareAsync(
            queue:      $"gw_{zone}",
            durable:    true,
            exclusive:  false,
            autoDelete: false);
        string queueName = qDeclare.QueueName;

        // Subscrever tópico: sensor.<zona>.# (todos os tipos desta zona)
        string bindingKey = $"sensor.{zone}.#";
        await channel.QueueBindAsync(queueName, exchangeName, bindingKey);
        Console.WriteLine($"[INFO] Subscrito: exchange={exchangeName} routing={bindingKey} queue={queueName}");

        // Consumer assíncrono
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                string json = Encoding.UTF8.GetString(ea.Body.ToArray());
                await HandleMessageAsync(json, ea.RoutingKey);
                await channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Erro ao processar mensagem: {ex.Message}");
                await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
            }
        };

        await channel.BasicConsumeAsync(queueName, autoAck: false, consumer);
        Console.WriteLine("[INFO] Gateway em escuta. Ctrl+C para terminar.");
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[WARN] RabbitMQ indisponível ({ex.Message}). A tentar em 5s...");
        await Task.Delay(5000);
    }
}

// ── Thread de agregação periódica (a cada 15 min ou 30s em demo) ─────────────
_ = Task.Run(async () =>
{
    int intervalSeconds = 30;   // 30s para demo; em prod usar 900 (15 min)
    Console.WriteLine($"[INFO] Agregação automática cada {intervalSeconds}s.");
    while (true)
    {
        await Task.Delay(intervalSeconds * 1000);
        await FlushAggregatesAsync();
    }
});

// Aguardar indefinidamente
await Task.Delay(Timeout.Infinite);

// ── Handler de mensagem recebida ──────────────────────────────────────────────
async Task HandleMessageAsync(string json, string routingKey)
{
    Console.WriteLine($"[SUB] {routingKey}: {json[..Math.Min(120, json.Length)]}");

    SensorMessage? msg;
    try
    {
        msg = JsonSerializer.Deserialize<SensorMessage>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
    catch
    {
        Console.WriteLine("[ERROR] JSON inválido recebido.");
        return;
    }

    if (msg == null) return;

    // ── Registar sensor ───────────────────────────────────────────────────────
    sensorManager.RegisterSensor(msg.SensorId, "rabbitmq", 0);

    // ── Chamar RPC de pré-processamento ───────────────────────────────────────
    var processed = await rpcPreprocessing.PreprocessDataAsync(
        msg.SensorId, msg.Type, msg.Value, msg.Unit, msg.Format ?? "JSON");

    if (processed == null) return;

    double normalizedValue = processed.NormalizedValue;
    string quality         = processed.Quality;
    bool   isOutlier       = processed.IsOutlier;

    // ── Persistir localmente (legado TP1) ─────────────────────────────────────
    var reading = new SensorReading
    {
        SensorId  = msg.SensorId,
        Type      = msg.Type,
        Value     = normalizedValue,
        Unit      = processed.NormalizedUnit,
        Quality   = quality,
        Timestamp = DateTime.UtcNow,
        IsOutlier = isOutlier,
        ZScore    = processed.ZScore
    };

    fileManager.AppendRawRecord(reading);
    databaseManager.InsertSensorReading(reading);

    sensorManager.UpdateLastDataTime(msg.SensorId);

    // ── Acrescentar ao buffer de agregação ────────────────────────────────────
    string aggKey = $"{msg.SensorId}:{msg.Type}";
    await aggLock.WaitAsync();
    try
    {
        if (!aggBuffer.ContainsKey(aggKey))
            aggBuffer[aggKey] = new List<(double, DateTime)>();
        aggBuffer[aggKey].Add((normalizedValue, DateTime.UtcNow));
    }
    finally
    {
        aggLock.Release();
    }

    // ── Envio imediato ao servidor (dado bruto) ───────────────────────────────
    try
    {
        serverForwarder.SendRawData(msg.SensorId, msg.Type, normalizedValue,
                                    processed.NormalizedUnit, DateTime.UtcNow);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[WARN] Falha ao encaminhar para servidor: {ex.Message}");
    }
}

// ── Flush de agregados para o servidor ───────────────────────────────────────
async Task FlushAggregatesAsync()
{
    await aggLock.WaitAsync();
    Dictionary<string, List<(double value, DateTime ts)>> snapshot;
    try
    {
        snapshot = new Dictionary<string, List<(double, DateTime)>>(aggBuffer);
        aggBuffer.Clear();
    }
    finally
    {
        aggLock.Release();
    }

    foreach (var (key, readings) in snapshot)
    {
        if (readings.Count == 0) continue;

        var parts    = key.Split(':');
        string sid   = parts[0];
        string dtype = parts[1];

        double avg = 0, mn = double.MaxValue, mx = double.MinValue;
        foreach (var (v, _) in readings)
        {
            avg += v;
            if (v < mn) mn = v;
            if (v > mx) mx = v;
        }
        avg /= readings.Count;

        var aggData = new AggregatedData
        {
            SensorId  = sid,
            Type      = dtype,
            Timestamp = DateTime.UtcNow,
            Period    = new AggregatedData.PeriodInfo
            {
                Start = readings[0].ts,
                End   = readings[^1].ts
            },
            Statistics = new AggregatedData.StatisticsInfo
            {
                Average = avg,
                Min     = mn,
                Max     = mx,
                Count   = readings.Count
            }
        };

        Console.WriteLine($"[AGG] {sid}/{dtype}: avg={avg:F2} min={mn:F2} max={mx:F2} n={readings.Count}");
        serverForwarder.SendAggregatedData(aggData);
    }
}

// ── Modelo de mensagem recebida do RabbitMQ ───────────────────────────────────
record SensorMessage(
    string SensorId,
    string Zone,
    string Type,
    double Value,
    string Unit,
    string Timestamp,
    string? Format
);
