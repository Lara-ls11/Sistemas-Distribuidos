# Estrutura de Ficheiros e Armazenamento

## 1. Estrutura de Diretórios

### 1.1 Gateway (Local)

```
TP1_Monitorizacao/
??? Gateway/
?   ??? data/
?   ?   ??? raw/                    # Dados brutos recebidos
?   ?   ?   ??? 2026-04-16/
?   ?   ?   ?   ??? GW001_14-30.json
?   ?   ?   ?   ??? GW001_14-45.json
?   ?   ?   ?   ??? GW001_15-00.json
?   ?   ?   ??? 2026-04-17/
?   ?   ?
?   ?   ??? aggregated/             # Dados agregados
?   ?       ??? 2026-04-16/
?   ?       ?   ??? TEMP_AGG_14-30.json
?   ?       ?   ??? TEMP_AGG_15-00.json
?   ?       ?   ??? HUM_AGG_14-30.json
?   ?       ?   ??? HUM_AGG_15-00.json
?   ?       ??? 2026-04-17/
?   ?
?   ??? logs/                       # Logs de operação
?   ?   ??? gateway_2026-04-16.log
?   ?   ??? gateway_2026-04-17.log
?   ?   ??? errors_2026-04-16.log
?   ?
?   ??? cache/                      # Cache em memória/ficheiro
?       ??? active_sensors.json     # Sensores activos
```

### 1.2 Servidor (Central)

```
TP1_Monitorizacao/
??? Servidor/
?   ??? data/
?   ?   ??? received/               # Dados recebidos do Gateway
?   ?   ?   ??? 2026-04-16/
?   ?   ?   ?   ??? 14-00_to_14-30.json
?   ?   ?   ?   ??? 14-30_to_15-00.json
?   ?   ?   ?   ??? 15-00_to_15-30.json
?   ?   ?   ??? 2026-04-17/
?   ?   ?
?   ?   ??? reports/                # Relatórios gerados
?   ?       ??? daily_2026-04-16.json
?   ?       ??? hourly_2026-04-16.json
?   ?       ??? summary_2026-04.json
?   ?
?   ??? logs/                       # Logs do servidor
?   ?   ??? server_2026-04-16.log
?   ?   ??? server_2026-04-17.log
?   ?   ??? errors_2026-04-16.log
?   ?
?   ??? index/                      # Índices para busca rápida
?       ??? data_index_2026-04.json
?       ??? sensor_index.json
```

---

## 2. Formato de Ficheiros

### 2.1 Ficheiros de Dados Brutos (Gateway)

**Nome:** `GW{ID}_{HH}-{MM}.json`
**Exemplo:** `GW001_14-30.json`

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
      "id": "REC_001",
      "sensorId": "SENSOR_001",
      "type": "TEMP",
      "value": 23.5,
      "unit": "C",
      "quality": "GOOD",
      "timestamp": "2026-04-16T14:30:00Z",
      "processed": false
    },
    {
      "id": "REC_002",
      "sensorId": "SENSOR_001",
      "type": "HUM",
      "value": 65.2,
      "unit": "%",
      "quality": "GOOD",
      "timestamp": "2026-04-16T14:30:00Z",
      "processed": false
    },
    {
      "id": "REC_003",
      "sensorId": "SENSOR_002",
      "type": "TEMP",
      "value": 22.8,
      "unit": "C",
      "quality": "FAIR",
      "timestamp": "2026-04-16T14:30:05Z",
      "processed": false
    }
  ],
  "recordCount": 3,
  "metadata": {
    "created": "2026-04-16T14:45:30Z",
    "lastModified": "2026-04-16T14:45:30Z",
    "version": "1.0"
  }
}
```

### 2.2 Ficheiros de Dados Agregados (Gateway)

**Nome:** `{TYPE}_AGG_{HH}-{MM}.json`
**Exemplo:** `TEMP_AGG_14-30.json`

```json
{
  "gatewayId": "GW001",
  "fileName": "TEMP_AGG_14-30.json",
  "type": "TEMP",
  "aggregationPeriod": 300,
  "period": {
    "start": "2026-04-16T14:00:00Z",
    "end": "2026-04-16T14:05:00Z"
  },
  "aggregations": [
    {
      "aggregationId": "AGG_001",
      "sensorId": "SENSOR_001",
      "type": "TEMP",
      "unit": "C",
      "statistics": {
        "count": 12,
        "average": 23.42,
        "minimum": 22.5,
        "maximum": 24.3,
        "sum": 281.04,
        "stdDev": 0.58
      },
      "quality": "GOOD",
      "timestamp": "2026-04-16T14:05:00Z"
    }
  ],
  "metadata": {
    "created": "2026-04-16T14:05:30Z",
    "version": "1.0"
  }
}
```

### 2.3 Ficheiros Recebidos no Servidor

**Nome:** `{HH}-{MM}_to_{HH}-{MM}.json`
**Exemplo:** `14-00_to_14-30.json`

```json
{
  "serverId": "SERVER_001",
  "fileName": "14-00_to_14-30.json",
  "period": {
    "start": "2026-04-16T14:00:00Z",
    "end": "2026-04-16T14:30:00Z"
  },
  "gateways": [
    {
      "gatewayId": "GW001",
      "dataReceived": "2026-04-16T14:35:00Z",
      "records": [
        {
          "id": "DATA_001",
          "sensorId": "SENSOR_001",
          "type": "TEMP",
          "value": 23.5,
          "unit": "C",
          "quality": "GOOD",
          "originalTimestamp": "2026-04-16T14:30:00Z",
          "receivedTimestamp": "2026-04-16T14:35:00Z",
          "status": "STORED"
        }
      ]
    }
  ],
  "metadata": {
    "created": "2026-04-16T14:35:00Z",
    "totalRecords": 1,
    "version": "1.0"
  }
}
```

### 2.4 Ficheiros de Registo (Logs)

**Nome:** `{component}_{YYYY-MM-DD}.log`
**Exemplo:** `gateway_2026-04-16.log`

```
2026-04-16 14:30:00 [INFO] Gateway started on port 5001
2026-04-16 14:30:15 [INFO] Sensor SENSOR_001 connected from 127.0.0.1:51234
2026-04-16 14:30:16 [DEBUG] Received: INIT
2026-04-16 14:30:16 [DEBUG] Sent: ACK_INIT
2026-04-16 14:30:17 [DEBUG] Received: CAPABILITIES:TEMP,HUM
2026-04-16 14:30:17 [DEBUG] Sent: ACK_CAPABILITIES
2026-04-16 14:30:18 [DEBUG] Received: DATA:TEMP:23.5:C:GOOD
2026-04-16 14:30:18 [INFO] Stored: TEMP=23.5°C from SENSOR_001
2026-04-16 14:30:18 [DEBUG] Sent: ACK_DATA
2026-04-16 14:30:19 [INFO] Sending to SERVER: STORE_DATA:TEMP:23.5:2026-04-16T14:30:18:GW001
2026-04-16 14:30:20 [DEBUG] Received from SERVER: ACK_STORE:DATA_001
2026-04-16 14:30:45 [INFO] Sensor SENSOR_001 disconnected
2026-04-16 14:30:45 [ERROR] Failed to connect to SERVER: Connection refused
```

### 2.5 Cache de Sensores Activos

**Nome:** `active_sensors.json`

```json
{
  "gatewayId": "GW001",
  "lastUpdated": "2026-04-16T14:30:45Z",
  "activeSensors": [
    {
      "sensorId": "SENSOR_001",
      "ipAddress": "127.0.0.1",
      "port": 51234,
      "capabilities": ["TEMP", "HUM"],
      "lastDataTime": "2026-04-16T14:30:40Z",
      "connected": true,
      "connectionTime": "2026-04-16T14:30:15Z"
    },
    {
      "sensorId": "SENSOR_002",
      "ipAddress": "127.0.0.1",
      "port": 51235,
      "capabilities": ["TEMP", "PRESS"],
      "lastDataTime": "2026-04-16T14:30:35Z",
      "connected": true,
      "connectionTime": "2026-04-16T14:29:50Z"
    }
  ]
}
```

---

## 3. Validações de Dados

### 3.1 Validações de Formato

| Campo | Regra | Exemplo Válido |
|-------|-------|-----------------|
| TYPE | Apenas A-Z, 0-9, _ | TEMP, HUM, PRESS |
| VALUE | Número (int ou float) | 23.5, 65, -10.2 |
| UNIT | Apenas A-Z, % | C, %, hPa, ppm |
| QUALITY | GOOD, FAIR, POOR | GOOD |
| TIMESTAMP | ISO 8601 | 2026-04-16T14:30:00Z |
| GATEWAY_ID | Apenas A-Z, 0-9, _ | GW001, GW_LAB_01 |

### 3.2 Intervalos de Valores Válidos

| Tipo | Mínimo | Máximo | Unidade | Nota |
|------|--------|--------|---------|------|
| TEMP | -50 | 50 | °C | Temperatura ambiente |
| HUM | 0 | 100 | % | Humidade relativa |
| PRESS | 300 | 1100 | hPa | Pressão atmosférica |
| LIGHT | 0 | 100000 | lux | Luminosidade |
| CO2 | 0 | 5000 | ppm | Dióxido de carbono |

### 3.3 Detecção de Outliers

**Método:** Desvio padrão (?)

```
Qualidade de dado:
- GOOD:  Dentro de [média - 2?, média + 2?]
- FAIR:  Dentro de [média - 3?, média + 3?]
- POOR:  Fora de [média - 3?, média + 3?]
```

### 3.4 Validações de Protocolo

1. **Ordem obrigatória:**
   - INIT ? ACK_INIT
   - CAPABILITIES ? ACK_CAPABILITIES
   - DATA* ? ACK_DATA (múltiplas vezes)
   - END ? ACK_END

2. **Sem transições inválidas:**
   - ? DATA antes de CAPABILITIES
   - ? CAPABILITIES após ACK_CAPABILITIES
   - ? INIT após INIT

3. **Comprimento máximo:** 1024 bytes por mensagem

4. **Timeout:** 5 segundos de inactividade

---

## 4. Política de Limpeza de Dados

### 4.1 Gateway

- **Dados brutos:** Manter 7 dias
- **Dados agregados:** Manter 30 dias
- **Logs:** Manter 14 dias
- **Cache:** Actualizar a cada conexão/desconexão

### 4.2 Servidor

- **Dados recebidos:** Manter 90 dias
- **Relatórios:** Arquivar após 1 ano
- **Logs:** Manter 30 dias
- **Índices:** Reconstruir semanalmente

---

## 5. Convenções de Nomenclatura

### 5.1 IDs Únicos

```
SENSOR_001, SENSOR_002, ...        # Sensores
GW001, GW002, ...                   # Gateways
SERVER_001, SERVER_002, ...         # Servidores
REC_000001, REC_000002, ...         # Registos
DATA_001, DATA_002, ...             # Dados armazenados
AGG_001, AGG_002, ...               # Agregações
```

### 5.2 Diretórios

- Datas em formato **YYYY-MM-DD**
- Horas em formato **HH-MM** ou **HH-MM-SS**
- Sem espaços, apenas hífens e underscores

---

## 6. Exemplo de Estrutura de Directório Criado

```bash
mkdir -p data/raw/{DATE}/
mkdir -p data/aggregated/{DATE}/
mkdir -p logs/
mkdir -p cache/

# DATA = 2026-04-16
```

---

## 7. Integração com Base de Dados (Futuro)

Quando implementada BD relacional, os mesmos dados serão armazenados em tabelas:

```
Tabelas Gateway:
- tbl_raw_readings      (id, sensor_id, type, value, unit, quality, timestamp)
- tbl_aggregations      (id, sensor_id, type, avg, min, max, count, period)
- tbl_active_sensors    (id, sensor_id, capabilities, last_update)

Tabelas Servidor:
- tbl_received_data     (id, gateway_id, sensor_id, type, value, timestamp)
- tbl_data_index        (id, date, gateway_id, record_count)
- tbl_reports           (id, report_type, date, data)
```
