/*
 * Sensor Urbano – TP2
 * Publica dados ambientais via RabbitMQ (Pub/Sub).
 * Tópico: sensor.<zona>.<tipo>   ex: sensor.zona1.TEMP
 * Exchange: sensors_exchange (tipo: topic)
 *
 * Modos de execução:
 *   auto  – simulação contínua automática de múltiplos tipos de sensor
 *   manual – menu interativo (comportamento TP1 adaptado)
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading;
using RabbitMQ.Client;

// ── Configuração ──────────────────────────────────────────────────────────────
string rabbitHost  = "localhost";
string exchangeName = "sensors_exchange";

// Argumentos: [zona] [modo: auto|manual] [host_rabbitmq]
string zone       = args.Length > 0 ? args[0] : "zona1";
string mode       = args.Length > 1 ? args[1] : "auto";
string host       = args.Length > 2 ? args[2] : rabbitHost;

string sensorId   = $"SENSOR_{zone.ToUpper()}_{Environment.ProcessId % 1000:000}";

Console.WriteLine("╔══════════════════════════════════════╗");
Console.WriteLine("║   SENSOR URBANO – Pub/Sub RabbitMQ   ║");
Console.WriteLine("╚══════════════════════════════════════╝");
Console.WriteLine($"[INFO] Sensor ID : {sensorId}");
Console.WriteLine($"[INFO] Zona      : {zone}");
Console.WriteLine($"[INFO] Modo      : {mode}");
Console.WriteLine($"[INFO] RabbitMQ  : {host}");
Console.WriteLine();

// ── Ligar ao RabbitMQ ─────────────────────────────────────────────────────────
IConnection? connection = null;
IChannel?    channel    = null;

while (true)
{
    try
    {
        var factory = new ConnectionFactory { HostName = host };
        connection  = await factory.CreateConnectionAsync();
        channel     = await connection.CreateChannelAsync();

        // Declarar exchange tipo "topic" (durável, não auto-delete)
        await channel.ExchangeDeclareAsync(
            exchange:    exchangeName,
            type:        ExchangeType.Topic,
            durable:     true,
            autoDelete:  false);

        Console.WriteLine("[INFO] Ligado ao RabbitMQ com sucesso.");
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[WARN] RabbitMQ não disponível ({ex.Message}). A tentar em 5s...");
        Thread.Sleep(5000);
    }
}

// ── Função de publicação ──────────────────────────────────────────────────────
async Task Publish(string dataType, double value, string unit)
{
    string routingKey = $"sensor.{zone}.{dataType.ToLower()}";

    var payload = new
    {
        sensorId,
        zone,
        type      = dataType,
        value,
        unit,
        timestamp = DateTime.UtcNow.ToString("o"),
        format    = "JSON"
    };

    string json  = JsonSerializer.Serialize(payload);
    byte[] body  = Encoding.UTF8.GetBytes(json);

    var props = new BasicProperties
    {
        ContentType  = "application/json",
        DeliveryMode = DeliveryModes.Persistent,
        Timestamp    = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
    };

    await channel!.BasicPublishAsync(
        exchange:   exchangeName,
        routingKey: routingKey,
        mandatory:  false,
        basicProperties: props,
        body:       body);

    Console.WriteLine($"[PUB] {routingKey} → {dataType}={value}{unit}  [{DateTime.UtcNow:HH:mm:ss}]");
}

// ── Modo automático ───────────────────────────────────────────────────────────
if (mode == "auto")
{
    Console.WriteLine("[INFO] Modo AUTO: a publicar dados simulados. Ctrl+C para terminar.");
    Console.WriteLine();

    var rng = new Random();
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    // Perfis de sensor por tipo
    var profiles = new Dictionary<string, (double baseVal, double noise, string unit)>
    {
        ["TEMP"]     = (22.0,  3.0,  "°C"),
        ["HUM"]      = (55.0,  10.0, "%"),
        ["PM25"]     = (18.0,  8.0,  "µg/m³"),
        ["NO2"]      = (40.0,  15.0, "µg/m³"),
        ["ACOUSTIC"] = (52.0,  8.0,  "dB"),
    };

    int cycle = 0;
    while (!cts.Token.IsCancellationRequested)
    {
        foreach (var (type, (baseVal, noise, unit)) in profiles)
        {
            double value = baseVal + (rng.NextDouble() - 0.5) * noise;
            value = Math.Round(value, 2);
            await Publish(type, value, unit);
            await Task.Delay(300, cts.Token);
        }

        cycle++;
        Console.WriteLine($"[INFO] Ciclo {cycle} concluído. Próximo em 10s...");
        await Task.Delay(10_000, cts.Token).ConfigureAwait(false);
    }

    Console.WriteLine("[INFO] Sensor terminado.");
}
// ── Modo manual (interativo) ──────────────────────────────────────────────────
else
{
    Console.WriteLine("[INFO] Modo MANUAL. Opções disponíveis:");

    bool running = true;
    while (running)
    {
        Console.WriteLine();
        Console.WriteLine("1 - Enviar leitura TEMP");
        Console.WriteLine("2 - Enviar leitura HUM");
        Console.WriteLine("3 - Enviar leitura PM25");
        Console.WriteLine("4 - Enviar leitura NO2");
        Console.WriteLine("5 - Enviar leitura ACOUSTIC");
        Console.WriteLine("6 - Simulação rápida (todos os tipos)");
        Console.WriteLine("0 - Sair");
        Console.Write("Opção: ");

        string op = Console.ReadLine() ?? "";

        switch (op)
        {
            case "1": await ReadAndPublish("TEMP",     "°C");     break;
            case "2": await ReadAndPublish("HUM",      "%");      break;
            case "3": await ReadAndPublish("PM25",     "µg/m³");  break;
            case "4": await ReadAndPublish("NO2",      "µg/m³");  break;
            case "5": await ReadAndPublish("ACOUSTIC", "dB");     break;
            case "6":
                var rng = new Random();
                await Publish("TEMP",     Math.Round(20 + rng.NextDouble() * 10, 1), "°C");
                await Publish("HUM",      Math.Round(40 + rng.NextDouble() * 40, 1), "%");
                await Publish("PM25",     Math.Round(5  + rng.NextDouble() * 30, 1), "µg/m³");
                await Publish("NO2",      Math.Round(20 + rng.NextDouble() * 60, 1), "µg/m³");
                await Publish("ACOUSTIC", Math.Round(45 + rng.NextDouble() * 20, 1), "dB");
                break;
            case "0":
                running = false;
                break;
            default:
                Console.WriteLine("Opção inválida.");
                break;
        }
    }

    Console.WriteLine("[INFO] Sensor terminado.");
}

await channel!.CloseAsync();
await connection!.CloseAsync();

// ── Auxiliar manual ───────────────────────────────────────────────────────────
async Task ReadAndPublish(string type, string unit)
{
    Console.Write($"Valor {type} ({unit}): ");
    string raw = (Console.ReadLine() ?? "").Trim().Replace(',', '.');
    if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
        await Publish(type, v, unit);
    else
        Console.WriteLine("Valor inválido.");
}
