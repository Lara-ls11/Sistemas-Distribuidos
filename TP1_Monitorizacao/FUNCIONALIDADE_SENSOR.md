# Funcionalidade de Operação SENSOR

## Visão Geral

Implementação da funcionalidade de operação SENSOR que permite ao **GATEWAY** receber dados dos sensores, realizar preprocessamento, agregação e encaminhamento para o servidor. A implementação inclui suporte a **base de dados relacional (SQLite com Entity Framework Core)** para persistência e análise de dados.

## Componentes Implementados

### 1. **Receção de Dados do Sensor**

O GATEWAY escuta na **porta 5001** para conexões de sensores. O protocolo segue a seguinte sequência:

```
1. INIT                          → ACK_INIT
2. CAPABILITIES:TEMP,HUM         → ACK_CAPABILITIES
3. DATA:TEMP:25.5                → ACK_DATA
4. END                           → ACK_END
```

**Recursos:**
- Suporte a múltiplos sensores simultâneos (threading)
- Validação de formato de mensagem
- Validação de tipo de dado (TEMP, HUM, PRESS, LIGHT, CO2)
- Validação de intervalo de valores
- Verificação de capabilities

### 2. **Preprocessamento de Dados**

- **Normalização**: Conversão de valores brutos em formato estruturado
- **Detecção de Outliers**: Usando Z-score com threshold configurável
- **Análise de Qualidade**: Classificação em GOOD, FAIR, POOR
- **Histórico**: Manutenção de histórico por sensor/tipo (últimas 100 leituras)
- **Estatísticas**: Cálculo de média, min, max, desvio padrão

### 3. **Armazenamento Multi-Camada**

#### A. Ficheiros (dados brutos em JSON)
```
data/raw/YYYY-MM-DD/
  ├── GW001_HH-MM.json  (15 minutos por ficheiro)
```

Estrutura do ficheiro:
```json
{
  "gatewayId": "GW001",
  "fileName": "GW001_14-00.json",
  "period": {
    "start": "2024-04-10T14:00:00Z",
    "end": "2024-04-10T14:15:00Z"
  },
  "records": [
    {
      "sensorId": "SENSOR_001",
      "type": "TEMP",
      "value": 25.5,
      "unit": "C",
      "quality": "GOOD",
      "timestamp": "2024-04-10T14:05:23Z",
      "zScore": 0.5,
      "isOutlier": false
    }
  ]
}
```

#### B. Base de Dados Relacional (SQLite)
- **SensorReadingEntity**: Armazena todas as leituras com metadados
- **DataAggregateEntity**: Armazena dados agregados e estado de envio

Índices para otimização:
- `SensorId`
- `Timestamp`
- `SensorId + Type + Timestamp`
- `SentToServer` (para rastreio de transmissão)

### 4. **Agregação de Dados**

Processa agregações a cada **15 minutos**:

```
Período: 15 minutos
Estatísticas calculadas:
  - Average (média)
  - Min (mínimo)
  - Max (máximo)
  - Count (número de leituras)
```

Exemplo de agregação:
```json
{
  "sensorId": "SENSOR_001",
  "type": "TEMP",
  "period": {
    "start": "2024-04-10T14:00:00Z",
    "end": "2024-04-10T14:15:00Z"
  },
  "stats": {
    "average": 25.3,
    "min": 24.8,
    "max": 26.1,
    "count": 12
  },
  "timestamp": "2024-04-10T14:15:00Z"
}
```

### 5. **Encaminhamento para Servidor**

Dois tipos de dados são encaminhados:

#### A. Dados Brutos
```
RAW_DATA|SENSOR_001|TEMP|25.5|C|2024-04-10T14:05:23Z
```

#### B. Dados Agregados
```
AGG_DATA|{JSON serializado com estrutura acima}
```

**Características:**
- Retry automático em caso de falha
- Teste de conectividade ao iniciar
- Logging de erros
- Rastreio de envios com flag `SentToServer` na BD

### 6. **Gerenciamento e Limpeza**

#### A. Limpeza de Sensores Inativos
- A cada 1 minuto, verifica sensores desconectados
- Remove do cache se não conectado há mais de 60 segundos

#### B. Limpeza de Ficheiros
- A cada 60 minutos, remove ficheiros com > 7 dias
- Calcula estatísticas de ficheiros (count, tamanho total)

#### C. Limpeza de Base de Dados
- A cada 60 minutos, remove registos com > 30 dias
- Mantém retenção de dados configurável

## Estrutura de Diretórios

```
TP1_Monitorizacao/
├── Gateway/
│   ├── Data/
│   │   └── SensorDbContext.cs        (Contexto EF Core)
│   ├── Managers/
│   │   ├── FileManager.cs            (Gestão de ficheiros)
│   │   ├── SensorManager.cs          (Gestão de sensores)
│   │   └── DatabaseManager.cs        (Gestão de BD)
│   ├── Models/
│   │   └── SensorInfo.cs             (Modelo de sensor)
│   ├── Services/
│   │   ├── DataValidator.cs          (Validação)
│   │   ├── DataPreprocessor.cs       (Pré-processamento)
│   │   ├── DataAggregationService.cs (Agregação)
│   │   └── ServerForwarderService.cs (Encaminhamento)
│   ├── Program.cs                    (Aplicação principal)
│   └── Gateway.csproj
├── Sensor/
│   ├── Program.cs
│   └── Sensor.csproj
├── Servidor/
│   ├── Program.cs
│   └── Servidor.csproj
└── data/
    ├── raw/                          (Ficheiros JSON)
    ├── logs/                         (Ficheiros de log)
    └── sensors.db                    (Base de dados SQLite)
```

## Base de Dados

### Schema

#### SensorReadingEntity
```sql
CREATE TABLE SensorReadings (
    Id INTEGER PRIMARY KEY,
    SensorId TEXT NOT NULL,
    Type TEXT NOT NULL,
    Value REAL NOT NULL,
    Unit TEXT,
    Quality TEXT,
    Timestamp DATETIME NOT NULL,
    ZScore REAL,
    IsOutlier BOOLEAN,
    CreatedAt DATETIME NOT NULL
);

CREATE INDEX IX_SensorId ON SensorReadings(SensorId);
CREATE INDEX IX_Timestamp ON SensorReadings(Timestamp);
CREATE INDEX IX_SensorIdTypeTimestamp ON SensorReadings(SensorId, Type, Timestamp);
```

#### DataAggregateEntity
```sql
CREATE TABLE DataAggregates (
    Id INTEGER PRIMARY KEY,
    SensorId TEXT NOT NULL,
    Type TEXT NOT NULL,
    PeriodStart DATETIME NOT NULL,
    PeriodEnd DATETIME NOT NULL,
    Average REAL NOT NULL,
    Min REAL NOT NULL,
    Max REAL NOT NULL,
    Count INTEGER NOT NULL,
    CreatedAt DATETIME NOT NULL,
    SentToServer BOOLEAN NOT NULL DEFAULT 0
);

CREATE INDEX IX_SensorId ON DataAggregates(SensorId);
CREATE INDEX IX_SensorIdPeriodStart ON DataAggregates(SensorId, PeriodStart);
CREATE INDEX IX_SentToServer ON DataAggregates(SentToServer);
```

## Fluxo de Operação

```
SENSOR conecta ao GATEWAY:5001
    ↓
GATEWAY cria thread de HandleSensor
    ↓
SENSOR envia INIT
    ↓
GATEWAY responde ACK_INIT
    ↓
SENSOR envia CAPABILITIES:TEMP,HUM
    ↓
GATEWAY valida e armazena capabilities
    ↓
SENSOR envia DATA:TEMP:25.5
    ↓
GATEWAY:
  1. Valida formato, tipo, intervalo
  2. Pré-processa (qualidade, outliers, histórico)
  3. Armazena em ficheiro JSON (preprocessamento)
  4. Persiste em BD (agregação futura)
  5. Encaminha para SERVIDOR (comunicação)
    ↓
A cada 15 minutos (Thread de Agregação):
  1. Recupera agregações pendentes da BD
  2. Serializa em JSON
  3. Encaminha para SERVIDOR
  4. Marca como enviadas
    ↓
SENSOR envia END
    ↓
GATEWAY encerra conexão
```

## Configurações

Todas as configurações estão codificadas como constantes nos serviços:

### Gateway/Data/SensorDbContext.cs
```csharp
private readonly string _connectionString; // data/sensors.db
```

### Gateway/Managers/FileManager.cs
```csharp
private readonly int _rotationIntervalMinutes = 15; // Rotação de ficheiros
```

### Gateway/Services/DataAggregationService.cs
```csharp
private readonly int _aggregationIntervalMinutes = 15; // Intervalo de agregação
```

### Gateway/Services/DataPreprocessor.cs
```csharp
private readonly int _maxHistorySize = 100; // Máximo de leituras em memória
```

### Detecção de Outliers
```csharp
double zScoreThreshold = 3.0; // Threshold para outliers
```

## Logs e Monitoramento

O Gateway produz logs estruturados com prefixos:
- `[INFO]` - Eventos importantes
- `[DEBUG]` - Detalhes de processamento
- `[WARN]` - Avisos (servidor indisponível, etc.)
- `[ERROR]` - Erros de processamento

Exemplo de saída:
```
[INFO] ========================================
[INFO] Funcionalidade de Operação SENSOR
[INFO] Gateway - Monitorização Distribuída
[INFO] ========================================
[INFO] Gateway iniciado na porta 5001
[INFO] À espera de sensores...
[INFO] Sensor conectado: 127.0.0.1:54321 (ID: SENSOR_001)
[INFO] SENSOR_001 iniciou conexão
[DEBUG] Gateway recebeu de SENSOR_001: INIT
[DEBUG] Gateway respondeu a SENSOR_001: ACK_INIT
[INFO] SENSOR_001 declarou capacidades: HUM, TEMP
[DEBUG] Gateway recebeu de SENSOR_001: DATA:TEMP:25.5
[INFO] SENSOR_001 enviou TEMP=25.5
[DEBUG] Leitura armazenada em ficheiro: SENSOR_001/TEMP=25.5C
[DEBUG] Leitura persistida em BD: SENSOR_001/TEMP
[DEBUG] Dados encaminhados para servidor: SENSOR_001/TEMP
[DEBUG] Gateway respondeu a SENSOR_001: ACK_DATA
```

## Tipos de Dados Suportados

| Tipo | Unidade | Intervalo | Descrição |
|------|---------|-----------|-----------|
| TEMP | °C | -50 a 50 | Temperatura Ambiente |
| HUM | % | 0 a 100 | Humidade Relativa |
| PRESS | hPa | 300 a 1100 | Pressão Atmosférica |
| LIGHT | lux | 0 a 100000 | Intensidade Luminosa |
| CO2 | ppm | 0 a 5000 | Dióxido de Carbono |

## APIs dos Serviços

### DatabaseManager
```csharp
bool InsertSensorReading(SensorReading reading)
List<SensorReadingEntity> GetSensorReadings(string sensorId, DateTime startTime, DateTime endTime)
DataAggregateEntity CalculateAggregate(string sensorId, string type, DateTime periodStart, DateTime periodEnd)
List<DataAggregateEntity> GetPendingAggregates()
bool MarkAggregateAsSent(int aggregateId)
(int totalReadings, DateTime? firstReading, DateTime? lastReading) GetSensorStatistics(string sensorId)
int CleanupOldRecords(int daysToKeep)
```

### ServerForwarderService
```csharp
bool SendRawData(string sensorId, string type, double value, string unit, DateTime timestamp)
bool SendAggregatedData(AggregatedData aggregated)
bool TestConnection()
```

### DataAggregationService
```csharp
AggregatedData ConvertToAggregatedData(DataAggregateEntity aggregate)
(DateTime start, DateTime end) GetCurrentAggregationPeriod()
(DateTime start, DateTime end) GetAggregationPeriodForTimestamp(DateTime timestamp)
string SerializeForTransmission(AggregatedData aggregated)
```

## Dependências Adicionadas

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />
```

## Próximas Melhorias (Sugestões)

1. **Compressão de Ficheiros**: Arquivar ficheiros antigos em formato ZIP
2. **Cache Distribuído**: Redis para cache de sensores ativos
3. **Alertas**: Sistema de alertas para valores fora do intervalo
4. **Dashboard**: Interface web para visualização de dados
5. **Replicação**: Backup automático de dados críticos
6. **Particionamento de BD**: Tabelas particionadas por data para melhor performance
7. **API REST**: Exposição de dados via API HTTP

## Notas de Implementação

- A base de dados é criada automaticamente no primeiro arranque
- Todos os timestamps são em UTC (DateTime.UtcNow)
- Thread-safe: Uso de locks para operações concorrentes
- Tolerância a falhas: Retry automático em falhas de conectividade
- Escalabilidade: Suporta múltiplos sensores com processamento paralelo

---

**Período de Implementação**: 7-10 de Abril
**Status**: ✅ Implementação Completa
**Versão**: 1.0
