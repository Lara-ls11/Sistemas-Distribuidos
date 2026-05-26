# TP2 – Serviços de Análise e Monitorização Urbana para One Health

## Arquitetura

```
SENSORES (C#) → RabbitMQ (Pub/Sub) → GATEWAYS (C#) → PreprocessingService (Python gRPC)
                                           ↓
                                    SERVIDOR (C#) ←── AnalysisService (Python gRPC)
                                           ↓
                                       SQLite BD
                                           ↓
                                      Interface CLI
```

## Pré-requisitos

- .NET 10 SDK
- Python 3.10+
- Docker Desktop (para RabbitMQ)

## Configuração inicial (uma vez)

### 1. Instalar Docker e iniciar RabbitMQ
```bash
docker-compose up -d rabbitmq
```
Gestão em: http://localhost:15672 (user: guest / pass: guest)

### 2. Instalar dependências Python e gerar ficheiros gRPC
```bat
setup_python.bat
```
Ou manualmente:
```bash
pip install grpcio grpcio-tools

cd PreprocessingService
python -m grpc_tools.protoc -I. --python_out=. --grpc_python_out=. preprocessing.proto

cd ../AnalysisService
python -m grpc_tools.protoc -I. --python_out=. --grpc_python_out=. analysis.proto
```

## Arranque do sistema

### Opção A: Script automático
```bat
start_tp2.bat
```

### Opção B: Manual (cada componente numa janela separada)

**Janela 1 – PreprocessingService (Python gRPC, porta 50051):**
```bash
cd PreprocessingService
python preprocessing_server.py
```

**Janela 2 – AnalysisService (Python gRPC, porta 50052):**
```bash
cd AnalysisService
python analysis_server.py
```

**Janela 3 – Servidor Principal (C#, porta 5002 TCP + CLI):**
```bash
cd Servidor
dotnet run
```

**Janela 4 – Gateway (C#, subscrive RabbitMQ zona1):**
```bash
cd Gateway
dotnet run zona1
# Argumentos: [zona] [rabbitmq_host] [preprocessing_host]
```

**Janela 5 – Sensor (C#, publica em RabbitMQ):**
```bash
cd Sensor
dotnet run zona1 auto
# Argumentos: [zona] [modo: auto|manual] [rabbitmq_host]
# modo auto  = simulação contínua de TEMP, HUM, PM25, NO2, ACOUSTIC
# modo manual = menu interativo
```

## Componentes

| Componente | Tecnologia | Porta | Função |
|---|---|---|---|
| Sensor | C# / .NET 10 | — | Publica dados via RabbitMQ |
| RabbitMQ | Docker | 5672 / 15672 | Broker Pub/Sub |
| Gateway | C# / .NET 10 | — | Subscrevê RabbitMQ, chama RPC pré-proc. |
| PreprocessingService | **Python** / gRPC | 50051 | Normaliza, valida, deteta outliers |
| Servidor | C# / .NET 10 | 5002 | Armazena em SQLite, CLI, chama RPC análise |
| AnalysisService | **Python** / gRPC | 50052 | Estatísticas, padrões, risco de saúde |

## Tópicos RabbitMQ

- Exchange: `sensors_exchange` (tipo: **topic**, durável)
- Routing key dos sensores: `sensor.<zona>.<tipo>` (ex: `sensor.zona1.temp`)
- Binding dos gateways: `sensor.<zona>.#` (todos os tipos dessa zona)

## Procedimentos RPC (gRPC)

### PreprocessingService (Gateway → Python)
- `PreprocessData(sensorId, dataType, value, unit)` → qualidade, outlier, z-score
- `ConvertFormat(rawData, sourceFormat)` → JSON normalizado

### AnalysisService (Servidor → Python)
- `AnalyzeReadings(sensorId, dataType, startTime, endTime)` → estatísticas + tendência
- `DetectPatterns(dataType, dataPoints[])` → padrões de poluição
- `PredictHealthRisk(zone, latestReadings[])` → nível de risco + recomendações

## Interface CLI do Servidor

Após iniciar o Servidor, o menu apresenta:

1. Listar sensores conhecidos
2. Consultar leituras (por sensor, tipo, período)
3. Estatísticas locais da BD
4. **Análise estatística via RPC** (chama AnalysisService)
5. **Deteção de padrões via RPC** (chama AnalysisService)
6. **Previsão de risco de saúde via RPC** (chama AnalysisService)
7. Ver resultados de análises anteriores
8. Exportar leituras para CSV
