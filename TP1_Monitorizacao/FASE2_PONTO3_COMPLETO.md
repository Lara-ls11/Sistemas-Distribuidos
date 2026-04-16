# Implementação Completada - FASE 2, Ponto 3

## ? Melhorar Tratamento de Múltiplos Sensores

### Resumo das Alterações

Foram implementadas as seguintes funcionalidades:

---

## 1. Classe `SensorInfo.cs`

**Localização:** `Gateway/Models/SensorInfo.cs`

Representa informações completas de um sensor conectado:

```csharp
public class SensorInfo
{
    public string SensorId { get; set; }           // ID único (SENSOR_001)
    public string IpAddress { get; set; }          // IP do sensor
    public int Port { get; set; }                  // Porta remota
    public List<string> Capabilities { get; set; } // Tipos de dados suportados
    public DateTime? LastDataTime { get; set; }    // Última leitura recebida
    public bool Connected { get; set; }            // Status de conexão
    public DateTime ConnectionTime { get; set; }   // Momento da conexão
    public int DataCount { get; set; }             // Contador de dados recebidos
    public int ErrorCount { get; set; }            // Contador de erros
}
```

**Funcionalidades:**
- ? Serialização JSON (JsonPropertyName)
- ? Construtores parametrizados
- ? Método ToString() para logging

---

## 2. Classe `SensorManager.cs`

**Localização:** `Gateway/Managers/SensorManager.cs`

Gerencia registros de sensores connectados com persistência.

### Métodos Principais:

| Método | Descrição |
|--------|-----------|
| `RegisterSensor(id, ip, port)` | Registra novo sensor |
| `GetSensor(id)` | Obtém sensor por ID |
| `UpdateCapabilities(id, caps)` | Atualiza tipos de dados |
| `UpdateLastDataTime(id)` | Atualiza último tempo de leitura |
| `IncrementErrorCount(id)` | Incrementa contador de erros |
| `DisconnectSensor(id)` | Marca como desconectado |
| `RemoveSensor(id)` | Remove do registo |
| `GetActiveSensors()` | Lista sensores conectados |
| `GetAllSensors()` | Lista todos os sensores |
| `CleanupInactiveSensors(min)` | Remove sensores inativos |

### Características:

- ? **Thread-safe:** Usa `ConcurrentDictionary` para operações seguras
- ? **Persistência:** Salva/carrega sensores em `cache/active_sensors.json`
- ? **Sincronização:** Lock para operações críticas de ficheiros
- ? **Cleanup automático:** Método para limpar sensores inativos
- ? **Logging:** Tratamento de exceções com mensagens detalhadas

---

## 3. Programa `Program.cs` Actualizado

**Localização:** `Gateway/Program.cs`

### Principais Melhorias:

#### A. Multi-Threading
```csharp
// Cada sensor conectado é processado em thread separada
Task.Run(() => HandleSensor(sensor, sensorId, remoteEndPoint));
```

**Vantagem:** Gateway agora aceita múltiplos sensores simultaneamente

#### B. Validação de Dados

```csharp
// Validar tipo declarado
if (!capabilities.Contains(type))
    return $"NACK_DATA:INVALID_TYPE";

// Validar intervalo de valores
if (!ValidateDataRange(type, value))
    return "NACK_DATA:VALUE_OUT_OF_RANGE";
```

**Intervalos:**
- TEMP: -50 a 50 °C
- HUM: 0 a 100 %
- PRESS: 300 a 1100 hPa
- LIGHT: 0 a 100000 lux
- CO2: 0 a 5000 ppm

#### C. Registo Estruturado com Logging Detalhado

```csharp
[INFO]  - Eventos importantes
[DEBUG] - Mensagens trocadas
[ERROR] - Erros e exceções
```

**Exemplo de logs:**
```
[INFO] Gateway iniciado na porta 5001
[INFO] Sensor conectado: 127.0.0.1:51234 (ID: SENSOR_001)
[DEBUG] Gateway recebeu de SENSOR_001: INIT
[DEBUG] Gateway respondeu a SENSOR_001: ACK_INIT
[INFO] SENSOR_001 iniciou conexão
[DEBUG] Gateway recebeu de SENSOR_001: CAPABILITIES:TEMP,HUM
[INFO] SENSOR_001 declarou capacidades: TEMP, HUM
[DEBUG] Gateway recebeu de SENSOR_001: DATA:TEMP:23.5:C:GOOD
[INFO] SENSOR_001 enviou TEMP=23.5
[DEBUG] Servidor respondeu: ACK_STORE:DATA_001
[INFO] Sensor SENSOR_001 desconectado.
```

#### D. Timeout de Leitura

```csharp
nsS.ReadTimeout = 5000; // 5 segundos
```

Detecta sensores que não respondem em tempo.

#### E. Thread de Limpeza Periódica

```csharp
// Executa a cada 1 minuto
Task.Run(() => CleanupThread());
```

Remove sensores inativos há mais de 60 minutos.

---

## 4. Ficheiro de Persistência

**Localização:** `cache/active_sensors.json`

Exemplo de conteúdo:
```json
{
  "gatewayId": "GW001",
  "lastUpdated": "2026-04-16T14:35:00Z",
  "activeSensors": [
    {
      "sensorId": "SENSOR_001",
      "ipAddress": "127.0.0.1",
      "port": 51234,
      "capabilities": ["HUM", "TEMP"],
      "lastDataTime": "2026-04-16T14:35:00Z",
      "connected": true,
      "connectionTime": "2026-04-16T14:30:15Z",
      "dataCount": 15,
      "errorCount": 0
    },
    {
      "sensorId": "SENSOR_002",
      "ipAddress": "127.0.0.1",
      "port": 51235,
      "capabilities": ["PRESS", "TEMP"],
      "lastDataTime": "2026-04-16T14:34:50Z",
      "connected": true,
      "connectionTime": "2026-04-16T14:29:50Z",
      "dataCount": 12,
      "errorCount": 0
    }
  ]
}
```

---

## 5. Fluxo de Processamento

```
???????????????????????????????????????????????????????????????????
? Gateway aguarda conexão (thread principal)                      ?
???????????????????????????????????????????????????????????????????
                              ?
                    Sensor conecta
                              ?
         ????????????????????????????????????????
         ? Gera ID: SENSOR_001                  ?
         ? Registra em SensorManager             ?
         ? Cria thread separada (HandleSensor)  ?
         ????????????????????????????????????????
                              ?
    ?????????????????????????????????????????????????????????????
    ? Thread 1: HandleSensor (SENSOR_001)                       ?
    ? - Aguarda INIT ? Responde ACK_INIT                        ?
    ? - Aguarda CAPABILITIES ? Valida ? ACK_CAPABILITIES       ?
    ? - Loop de DATA:                                           ?
    ?   * Valida tipo, valor, intervalo                        ?
    ?   * Envia para SERVIDOR                                   ?
    ?   * Responde ACK_DATA                                     ?
    ? - Aguarda END ? ACK_END ? Fecha conexão                  ?
    ?????????????????????????????????????????????????????????????
                              ?
         ????????????????????????????????????????
         ? Marca como desconectado              ?
         ? Atualiza active_sensors.json         ?
         ? Liberta recursos                     ?
         ????????????????????????????????????????
```

---

## 6. Teste de Funcionalidade

### Para testar a implementação:

**Terminal 1 - Gateway:**
```bash
cd TP1_Monitorizacao/Gateway
dotnet run
```

**Terminal 2 - Sensor:**
```bash
cd TP1_Monitorizacao/Sensor
dotnet run
```

**Terminal 3 - Servidor:**
```bash
cd TP1_Monitorizacao/Servidor
dotnet run
```

**Esperado:**
- Gateway aceita múltiplos sensores
- Cada sensor é processado em thread separada
- Logs estruturados mostram fluxo de dados
- Ficheiro `cache/active_sensors.json` é criado e atualizado
- Sensores aparecem/desaparecem do registo dinamicamente

---

## 7. Melhorias Implementadas vs Especificação

| Requisito | Status | Detalhes |
|-----------|--------|----------|
| Multi-threading | ? | Task.Run para cada sensor |
| SensorInfo class | ? | Completo com JSON serialization |
| SensorManager | ? | Thread-safe com persistência |
| Validação de dados | ? | Formato, tipo, intervalo |
| Logging estruturado | ? | [INFO], [DEBUG], [ERROR] |
| Timeout detection | ? | 5 segundos ReadTimeout |
| Cleanup automático | ? | Thread periódica |
| Persistência | ? | active_sensors.json |

---

## 8. Próximos Passos (FASE 2, Ponto 4)

**Implementar camada de pré-processamento:**
- [ ] Criar classe `DataValidator`
- [ ] Criar classe `DataPreprocessor`
- [ ] Adicionar detecção de outliers
- [ ] Adicionar histórico de valores
- [ ] Armazenar em ficheiros `data/raw/{DATE}/`

---

## Compiltação

? **Build Status:** SUCESSO COM AVISOS
- Gateway: OK
- Sensor: OK
- Servidor: OK

**Avisos de nullability (non-critical):**
- SensorInfo propriedades que poderiam ser nullable
- Métodos que podem retornar null

Estes avisos não afectam funcionamento, apenas segurança de tipo.

---

**Data:** 16 de Abril de 2026
**Status:** ? PONTO 3 COMPLETO
**Próximo:** PONTO 4 - Camada de Pré-processamento
