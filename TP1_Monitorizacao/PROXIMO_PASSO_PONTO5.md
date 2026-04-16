# ?? Próximo Passo: PONTO 5 - Armazenamento em Ficheiros

## O que fazer agora?

Implementar a classe `FileManager` que persistirá as leituras em ficheiros JSON.

---

## Especificação do FileManager

### Localização
```
Gateway/Services/FileManager.cs
```

### Estrutura de Directórios
```
Gateway/
??? data/
    ??? raw/                    # Dados brutos
    ?   ??? {YYYY-MM-DD}/
    ?       ??? GW001_14-00.json
    ?       ??? GW001_14-15.json
    ?       ??? GW001_14-30.json
    ?
    ??? logs/                   # Logs de ficheiros
        ??? file_operations_{YYYY-MM-DD}.log
```

### Classes a Implementar

```csharp
public class FileManager
{
    // Propriedades
    private string _baseDataDir = "data";
    private string _rawDataDir = "data/raw";
    private int _rotationIntervalMinutes = 15;
    private int _maxRecordsPerFile = 1000;
    
    // Métodos principais
    public void EnsureDirectoriesExist()
    public string GetCurrentDataFile(string gatewayId)
    public void AppendRawRecord(SensorReading reading)
    public List<SensorReading> ReadRawRecords(DateTime date, string gatewayId)
    public void ArchiveFile(string filePath)
    public void CleanupOldFiles(int daysToKeep = 7)
}

public class RawDataFile
{
    public string GatewayId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public List<SensorReading> Records { get; set; }
    public int RecordCount { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
}
```

### Métodos a Implementar

#### 1. `EnsureDirectoriesExist()`
```csharp
// Criar directórios necessários
// - data/
// - data/raw/
// - data/raw/{TODAY}/
// - logs/
```

#### 2. `GetCurrentDataFile(gatewayId)`
```csharp
// Retorna nome do ficheiro actual baseado na hora
// Formato: {YYYYMMDD}/{gatewayId}_{HH-MM}.json
// Exemplo: 20260416/GW001_14-30.json

// Lógica:
// - Obter hora actual
// - Arredondar para intervalo de 15 minutos
// - Construir path
// - Criar ficheiro se não existir
```

#### 3. `AppendRawRecord(reading)`
```csharp
// Adicionar uma leitura ao ficheiro atual
// 1. Obter ficheiro atual
// 2. Ler JSON existente (se houver)
// 3. Adicionar nova leitura
// 4. Incrementar contador
// 5. Guardar JSON formatado
// 6. Log da operação

// Arquivo JSON:
{
  "gatewayId": "GW001",
  "fileName": "GW001_14-30.json",
  "period": {
    "start": "2026-04-16T14:30:00Z",
    "end": "2026-04-16T14:45:00Z"
  },
  "records": [
    {
      "sensorId": "SENSOR_001",
      "type": "TEMP",
      "value": 23.5,
      "unit": "C",
      "quality": "GOOD",
      "timestamp": "2026-04-16T14:30:00Z",
      "zScore": 0.15,
      "isOutlier": false,
      "processed": false
    }
  ],
  "recordCount": 1,
  "metadata": {
    "created": "2026-04-16T14:30:00Z",
    "lastModified": "2026-04-16T14:31:00Z",
    "version": "1.0"
  }
}
```

#### 4. `ReadRawRecords(date, gatewayId)`
```csharp
// Ler registos de um dia específico
// Retorna List<SensorReading> de todos os ficheiros do dia
```

#### 5. `CleanupOldFiles(daysToKeep)`
```csharp
// Remover ficheiros com mais de X dias
// Padrão: 7 dias (conforme especificação)
// Log: Registar ficheiros removidos
```

### Integração no Gateway.Program.cs

No método `HandleSensor`, após pré-processar:

```csharp
// Adicionar isto após PreprocessReading
var reading = _preprocessor.PreprocessReading(...);

// Guardar em ficheiro
_fileManager.AppendRawRecord(reading);
```

---

## Padrão de Ficheiros

### Exemplo de Progressão

```
14:00 - 14:15
??? GW001_14-00.json
    Records: [REC_001, REC_002, REC_003] (3 registos)
    
14:15 - 14:30
??? GW001_14-15.json
    Records: [REC_004, REC_005, REC_006, REC_007] (4 registos)
    
14:30 - 14:45
??? GW001_14-30.json
    Records: [REC_008, REC_009] (2 registos)
```

### Conteúdo Completo de um Ficheiro

```json
{
  "gatewayId": "GW001",
  "fileName": "GW001_14-30.json",
  "period": {
    "start": "2026-04-16T14:30:00Z",
    "end": "2026-04-16T14:45:00Z"
  },
  "records": [
    {
      "sensorId": "SENSOR_001",
      "type": "TEMP",
      "value": 23.5,
      "unit": "C",
      "quality": "GOOD",
      "timestamp": "2026-04-16T14:30:00Z",
      "zScore": 0.15,
      "isOutlier": false,
      "processed": false
    },
    {
      "sensorId": "SENSOR_001",
      "type": "HUM",
      "value": 65.2,
      "unit": "%",
      "quality": "GOOD",
      "timestamp": "2026-04-16T14:30:00Z",
      "zScore": -0.05,
      "isOutlier": false,
      "processed": false
    },
    {
      "sensorId": "SENSOR_002",
      "type": "TEMP",
      "value": 22.8,
      "unit": "C",
      "quality": "GOOD",
      "timestamp": "2026-04-16T14:30:05Z",
      "zScore": 0.08,
      "isOutlier": false,
      "processed": false
    }
  ],
  "recordCount": 3,
  "metadata": {
    "created": "2026-04-16T14:30:00Z",
    "lastModified": "2026-04-16T14:35:30Z",
    "version": "1.0"
  }
}
```

---

## Considerações de Implementação

### 1. Thread-Safety
- Usar `lock` quando escrever ficheiros
- ConcurrentDictionary para cache de ficheiros abertos

### 2. Performance
- Cache de ficheiros em memória
- Não re-ler ficheiro a cada inserção
- Escritas em batch (se possível)

### 3. Limpeza
- Task.Run() para CleanupOldFiles (background)
- Executa a cada hora

### 4. Logging
- Registar todas as operações
- Ficheiro: `data/logs/file_operations_{DATE}.log`

### 5. Tratamento de Erros
- Criar directórios se não existirem
- Exceções bem documentadas
- Fallback se ficheiro corrompido

---

## Pseudocódigo Principal

```
Timer a cada 15 minutos:
  - Se hora mudou de período:
    a. Fechar ficheiro anterior
    b. Criar novo ficheiro
    c. Actualizar metadados

Ao receber leitura:
  - Obter ficheiro actual
  - Lock:
    a. Ler ficheiro (se existir)
    b. Adicionar leitura
    c. Escrever ficheiro
  - Log: sucesso/erro

A cada hora:
  - CleanupOldFiles(7 dias)
  - Log: ficheiros removidos
```

---

## Testes Esperados

Após implementação:

```bash
# Terminal 1-3: Servidor, Gateway, Sensor
# (conforme antes)

# Verificar ficheiros criados
ls -la TP1_Monitorizacao/Gateway/data/raw/2026-04-16/
# Esperado: GW001_14-00.json, GW001_14-15.json, etc

# Ver conteúdo
cat TP1_Monitorizacao/Gateway/data/raw/2026-04-16/GW001_14-00.json | jq .
# Esperado: JSON bem formado com recordCount > 0
```

---

## Estimativa

- **Linhas de Código:** 300-400
- **Tempo de Implementação:** 45-60 minutos
- **Complexidade:** Média
- **Testes:** 15-20 minutos

---

## Próxima Fase Após Ponto 5

Após completar FileManager:

### Ponto 6: Agregação de Dados
- Criar classe `AggregationEngine`
- Ler ficheiros de `data/raw/`
- Calcular agregações (5 min padrão)
- Escrever para `data/aggregated/`
- Atualizar flag `processed` nos registos

---

## Recursos de Referência

```
Documentação:
- ESTRUTURA_FICHEIROS.md     # Formato de ficheiros
- PROTOCOLO_COMUNICACAO.md   # Estrutura de dados
- DataPreprocessor.cs        # SensorReading class

Exemplos de JSON:
- cache/active_sensors.json  # Já existe no Gateway
```

---

**Próximo Passo:** Implementar `Gateway/Services/FileManager.cs`
**Tempo Recomendado:** 1 hora
**Dificuldade:** Média
**Prioritário:** SIM (obrigatório para funcionamento)

Quer que comece com a implementação? ??
