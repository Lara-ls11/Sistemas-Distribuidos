# 📚 REFERÊNCIA COMPLETA - Funcionalidade de Operação SENSOR

## 📍 Localização dos Ficheiros

### Código Principal

```
TP1_Monitorizacao/
├── Gateway/
│   ├── Data/
│   │   └── SensorDbContext.cs              ← Novo (BD Context)
│   ├── Managers/
│   │   ├── DatabaseManager.cs              ← Novo (BD Manager)
│   │   ├── FileManager.cs                  (existente)
│   │   └── SensorManager.cs                (existente)
│   ├── Services/
│   │   ├── DataAggregationService.cs       ← Novo (Agregação)
│   │   ├── DataPreprocessor.cs             (existente)
│   │   ├── DataValidator.cs                (existente)
│   │   └── ServerForwarderService.cs       ← Novo (Forwarder)
│   ├── Models/
│   │   └── SensorInfo.cs                   (existente)
│   ├── Program.cs                          ← Modificado
│   └── Gateway.csproj                      ← Modificado
├── Sensor/
│   └── Program.cs                          (não modificado)
├── Servidor/
│   └── Program.cs                          (não modificado)
└── data/                                   (criado em runtime)
    ├── raw/                                (ficheiros JSON)
    ├── logs/                               (logs)
    └── sensors.db                          (SQLite BD)
```

## 🔧 Configurações Importantes

### Portas
- **Gateway (listening)**: 5001
- **Servidor (listening)**: 5002
- **Gateway (connect para servidor)**: localhost:5002

### Intervalos
- **Rotação de ficheiros**: 15 minutos
- **Agregação de dados**: 15 minutos
- **Limpeza de sensores**: 1 minuto
- **Limpeza de ficheiros**: 60 minutos (remove >7 dias)
- **Limpeza de BD**: 60 minutos (remove >30 dias)

### Limites
- **Tamanho máximo de mensagem**: 1024 bytes
- **Timeout de conexão**: 5 segundos
- **Máximo histórico por sensor**: 100 leituras
- **Threshold Z-score**: 3.0 (outliers)

## 🗂️ Estrutura de Base de Dados

### Tabela: SensorReadings
```sql
CREATE TABLE SensorReadings (
    Id              INTEGER PRIMARY KEY,
    SensorId        TEXT NOT NULL,
    Type            TEXT NOT NULL,
    Value           REAL NOT NULL,
    Unit            TEXT,
    Quality         TEXT,
    Timestamp       DATETIME NOT NULL,
    ZScore          REAL,
    IsOutlier       BOOLEAN,
    CreatedAt       DATETIME NOT NULL
);

CREATE INDEX IX_SensorReadings_SensorId ON SensorReadings(SensorId);
CREATE INDEX IX_SensorReadings_Timestamp ON SensorReadings(Timestamp);
CREATE INDEX IX_SensorReadings_Complex ON SensorReadings(SensorId, Type, Timestamp);
```

### Tabela: DataAggregates
```sql
CREATE TABLE DataAggregates (
    Id              INTEGER PRIMARY KEY,
    SensorId        TEXT NOT NULL,
    Type            TEXT NOT NULL,
    PeriodStart     DATETIME NOT NULL,
    PeriodEnd       DATETIME NOT NULL,
    Average         REAL NOT NULL,
    Min             REAL NOT NULL,
    Max             REAL NOT NULL,
    Count           INTEGER NOT NULL,
    CreatedAt       DATETIME NOT NULL,
    SentToServer    BOOLEAN NOT NULL DEFAULT 0
);

CREATE INDEX IX_DataAggregates_SensorId ON DataAggregates(SensorId);
CREATE INDEX IX_DataAggregates_Period ON DataAggregates(SensorId, PeriodStart);
CREATE INDEX IX_DataAggregates_SentToServer ON DataAggregates(SentToServer);
```

## 📋 APIs e Métodos Principais

### DatabaseManager
```csharp
// Inserção
bool InsertSensorReading(SensorReading reading)

// Consulta
List<SensorReadingEntity> GetSensorReadings(
    string sensorId, 
    DateTime startTime, 
    DateTime endTime
)

// Agregação
DataAggregateEntity CalculateAggregate(
    string sensorId, 
    string type, 
    DateTime periodStart, 
    DateTime periodEnd
)

// Rastreio
List<DataAggregateEntity> GetPendingAggregates()
bool MarkAggregateAsSent(int aggregateId)

// Estatísticas
(int totalReadings, DateTime? firstReading, DateTime? lastReading) 
GetSensorStatistics(string sensorId)

// Limpeza
int CleanupOldRecords(int daysToKeep = 30)
```

### ServerForwarderService
```csharp
// Envio de dados
bool SendRawData(
    string sensorId, 
    string type, 
    double value, 
    string unit, 
    DateTime timestamp
)

bool SendAggregatedData(AggregatedData aggregated)

// Conectividade
bool TestConnection()
```

### DataAggregationService
```csharp
// Conversão
AggregatedData ConvertToAggregatedData(
    DataAggregateEntity aggregate
)

// Períodos
(DateTime start, DateTime end) GetCurrentAggregationPeriod()
(DateTime start, DateTime end) GetAggregationPeriodForTimestamp(
    DateTime timestamp
)

// Serialização
string SerializeForTransmission(AggregatedData aggregated)
```

## 📤 Protocolo de Comunicação

### Inicialização

```
CLIENT: INIT
SERVER: ACK_INIT
```

### Declaração de Capacidades

```
CLIENT: CAPABILITIES:TEMP,HUM,PRESS
SERVER: ACK_CAPABILITIES
```

Resposta de erro:
```
SERVER: NACK_CAPABILITIES:INVALID_FORMAT
```

### Transmissão de Dados

```
CLIENT: DATA:TEMP:25.5
SERVER: ACK_DATA
```

Respostas de erro:
```
SERVER: NACK_DATA:Tipo_não_suportado
SERVER: NACK_DATA:Valor_fora_do_intervalo
SERVER: NACK_DATA:Tipo_não_declarado_em_CAPABILITIES
```

### Encerramento

```
CLIENT: END
SERVER: ACK_END
```

### Encaminhamento ao Servidor

Dados brutos:
```
RAW_DATA|SENSOR_001|TEMP|25.5|C|2024-04-10T14:05:23Z
```

Dados agregados:
```
AGG_DATA|{
  "sensorId": "SENSOR_001",
  "type": "TEMP",
  "period": {"start": "...", "end": "..."},
  "stats": {"average": 25.3, "min": 24.8, "max": 26.1, "count": 12}
}
```

## 🎯 Tipos de Dados

### Campos de SensorReading

| Campo | Tipo | Descrição |
|-------|------|-----------|
| SensorId | string | ID único do sensor (ex: SENSOR_001) |
| Type | string | Tipo de dado (TEMP, HUM, PRESS, LIGHT, CO2) |
| Value | double | Valor da leitura |
| Unit | string | Unidade (°C, %, hPa, lux, ppm) |
| Quality | string | Classificação (GOOD, FAIR, POOR) |
| Timestamp | DateTime | Quando foi lido (UTC) |
| ZScore | double? | Z-score para detecção de outliers |
| IsOutlier | boolean | Se é considerado outlier |

### Intervalos Válidos por Tipo

| Tipo | Mínimo | Máximo | Unidade |
|------|--------|--------|---------|
| TEMP | -50 | 50 | °C |
| HUM | 0 | 100 | % |
| PRESS | 300 | 1100 | hPa |
| LIGHT | 0 | 100000 | lux |
| CO2 | 0 | 5000 | ppm |

## 📊 Formato de Ficheiros JSON

### Estrutura Completa

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
      "timestamp": "2024-04-10T14:05:23.123Z",
      "zScore": 0.45,
      "isOutlier": false
    }
  ],
  "recordCount": 1,
  "metadata": {
    "created": "2024-04-10T14:00:00Z",
    "lastModified": "2024-04-10T14:08:00Z",
    "version": "1.0"
  }
}
```

## 🔐 Thread Safety

### Sincronização

- **DatabaseManager**: Lock privado `_lockObject` por instância
- **FileManager**: Lock privado `_lockObject` por instância
- **SensorManager**: ConcurrentDictionary para thread-safe
- **DataPreprocessor**: Dictionary privado (thread-local por instância)

### Operações Críticas Protegidas

- Inserção em BD
- Atualização de ficheiros
- Limpeza de dados antigos
- Atualização de sensores ativos

## 🎨 Padrões de Design Utilizados

- **Singleton**: DatabaseManager, FileManager, SensorManager (inicializados uma vez)
- **Factory**: Criação de SensorReading, DataAggregateEntity
- **Observer**: Threads de cleanup e agregação monitoram estado
- **Strategy**: DataValidator, DataPreprocessor com diferentes estratégias

## 🧪 Exemplos de Queries SQL

### Última leitura por sensor

```sql
SELECT 
    SensorId,
    Type,
    Value,
    Timestamp
FROM SensorReadings
WHERE (SensorId, Timestamp) IN (
    SELECT SensorId, MAX(Timestamp)
    FROM SensorReadings
    GROUP BY SensorId
)
ORDER BY SensorId;
```

### Estatísticas por hora

```sql
SELECT 
    SensorId,
    Type,
    DATE(Timestamp) as Date,
    HOUR(Timestamp) as Hour,
    COUNT(*) as Count,
    ROUND(AVG(Value), 2) as Average,
    MIN(Value) as Minimum,
    MAX(Value) as Maximum
FROM SensorReadings
GROUP BY SensorId, Type, DATE(Timestamp), HOUR(Timestamp)
ORDER BY Date DESC, Hour DESC;
```

### Dados não enviados

```sql
SELECT *
FROM DataAggregates
WHERE SentToServer = 0
ORDER BY CreatedAt ASC;
```

### Outliers detectados

```sql
SELECT 
    SensorId,
    Type,
    Value,
    ROUND(ZScore, 2) as ZScore,
    Quality,
    Timestamp
FROM SensorReadings
WHERE IsOutlier = 1
ORDER BY ABS(ZScore) DESC;
```

## 🚀 Eventos e Logs

### Eventos Principais

| Evento | Log | Descrição |
|--------|-----|-----------|
| Sensor conecta | [INFO] | Novo ID atribuído |
| INIT recebido | [INFO] | Inicialização reconhecida |
| Capabilities | [INFO] | Tipos declarados |
| Dados recebidos | [DEBUG] | Leitura armazenada |
| Erro validação | [ERROR] | Mensagem rejeitada |
| Servidor indisponível | [WARN] | Retry automático |
| Agregação | [INFO] | Dados agregados e enviados |
| Limpeza | [INFO] | Dados antigos removidos |

## 📞 Debugging

### Logs Importantes

```
[INFO] Gateway iniciado               → Sistema pronto
[DEBUG] Gateway recebeu de SENSOR_X   → Mensagem recebida
[DEBUG] Leitura armazenada            → Ficheiro JSON atualizado
[DEBUG] Leitura persistida            → BD atualizada
[INFO] AGREGAÇÃO DE DADOS             → Agregação iniciada
[ERROR] Falha ao enviar               → Problema de conectividade
```

### Verificação de Dados

```bash
# Ver ficheiros criados
find data/raw -name "*.json" -type f

# Contar registos em BD
sqlite3 data/sensors.db "SELECT COUNT(*) FROM SensorReadings;"

# Ver últimos registos
sqlite3 data/sensors.db "SELECT * FROM SensorReadings ORDER BY CreatedAt DESC LIMIT 10;"
```

---

**Última atualização**: Abril 2024
**Versão**: 1.0
**Status**: ✅ Implementação Completa
