# Protocolo de Comunicação SENSOR-GATEWAY-SERVIDOR

## 1. Visão Geral

Sistema de comunicação baseado em TCP entre três componentes:
- **SENSOR**: Dispositivo que coleta dados (temperatura, humidade, etc.)
- **GATEWAY**: Intermediário que recebe, processa e agrega dados
- **SERVIDOR**: Armazena e persiste os dados finais

---

## 2. Fluxo de Comunicação

```
SENSOR ?? GATEWAY ?? SERVIDOR
(5001)    (5001)?(5002)
```

---

## 3. PROTOCOLO SENSOR ? GATEWAY

### 3.1 Conexão Inicial

#### Mensagem: INIT
**Enviada por:** SENSOR
**Recebida por:** GATEWAY

```
SENSOR ? GATEWAY: INIT
GATEWAY ? SENSOR: ACK_INIT
```

**Propósito:** Iniciar conexão e validar disponibilidade
**Timeout:** 5 segundos

---

### 3.2 Declaração de Capacidades

#### Mensagem: CAPABILITIES
**Enviada por:** SENSOR
**Recebida por:** GATEWAY

```
Formato: CAPABILITIES:<TYPE1>,<TYPE2>,...,<TYPEN>

Exemplo:
SENSOR ? GATEWAY: CAPABILITIES:TEMP,HUM
GATEWAY ? SENSOR: ACK_CAPABILITIES
```

**Tipos de dados suportados:**
- `TEMP` - Temperatura (°C)
- `HUM` - Humidade relativa (%)
- `PRESS` - Pressão (hPa)
- `LIGHT` - Luminosidade (lux)
- `CO2` - Dióxido de carbono (ppm)

**Validações:**
- Sensor pode declarar múltiplos tipos
- Tipo inválido ? rejeitar com `NACK_CAPABILITIES`
- Duplicatas ? remover e aceitar

---

### 3.3 Envio de Dados

#### Mensagem: DATA
**Enviada por:** SENSOR
**Recebida por:** GATEWAY

```
Formato: DATA:<TYPE>:<VALUE>:[UNIT]:[QUALITY]

Obrigatório:
- TYPE: Tipo de dado (ex: TEMP, HUM)
- VALUE: Valor numérico (inteiro ou decimal)

Opcional:
- UNIT: Unidade do valor (ex: C, %, hPa)
- QUALITY: Qualidade da leitura (GOOD, FAIR, POOR)

Exemplos:
DATA:TEMP:23.5
DATA:TEMP:23.5:C:GOOD
DATA:HUM:65:PERCENT:GOOD
```

**Resposta esperada:**
```
GATEWAY ? SENSOR: ACK_DATA
ou
GATEWAY ? SENSOR: NACK_DATA:<REASON>
```

**Razões de NACK:**
- `INVALID_TYPE` - Tipo de dado não declarado em CAPABILITIES
- `INVALID_VALUE` - Valor fora do intervalo válido
- `INVALID_FORMAT` - Formato não reconhecido
- `PROCESSING_ERROR` - Erro ao processar dados

**Validações:**
- Formato correto (presença de : como separador)
- Tipo declarado anteriormente em CAPABILITIES
- Valor numérico válido
- Intervalo de valores:
  - TEMP: -50 a 50 °C
  - HUM: 0 a 100 %
  - PRESS: 300 a 1100 hPa
  - LIGHT: 0 a 100000 lux
  - CO2: 0 a 5000 ppm

**Intervalo de envio:**
- Sensor pode enviar múltiplos dados
- Sem limite de frequência (limitado apenas pela rede)

---

### 3.4 Encerramento de Conexão

#### Mensagem: END
**Enviada por:** SENSOR
**Recebida por:** GATEWAY

```
SENSOR ? GATEWAY: END
GATEWAY ? SENSOR: ACK_END
```

**Propósito:** Encerrar conexão graciosamente
**Comportamento:**
- Gateway fecha conexão após ACK_END
- Sensor aguarda ACK_END e fecha conexão

---

## 4. PROTOCOLO GATEWAY ? SERVIDOR

### 4.1 Armazenamento de Dados

#### Mensagem: STORE_DATA
**Enviada por:** GATEWAY
**Recebida por:** SERVIDOR

```
Formato: STORE_DATA:<TYPE>:<VALUE>:[TIMESTAMP]:[GATEWAY_ID]

Obrigatório:
- TYPE: Tipo de dado
- VALUE: Valor numérico

Opcional:
- TIMESTAMP: Momento da coleta (ISO 8601: YYYY-MM-DDTHH:mm:ss)
- GATEWAY_ID: Identificador do gateway

Exemplos:
STORE_DATA:TEMP:23.5:2026-04-16T14:30:00:GW001
STORE_DATA:HUM:65:2026-04-16T14:30:00:GW001
```

**Resposta esperada:**
```
SERVIDOR ? GATEWAY: ACK_STORE:<DATA_ID>
ou
SERVIDOR ? GATEWAY: NACK_STORE:<REASON>
```

---

### 4.2 Envio de Dados Agregados

#### Mensagem: AGGREGATE_DATA
**Enviada por:** GATEWAY (futuro)
**Recebida por:** SERVIDOR

```
Formato: AGGREGATE_DATA:<TYPE>:<AVG>:<MIN>:<MAX>:<COUNT>:[PERIOD]:[TIMESTAMP]

Exemplo:
AGGREGATE_DATA:TEMP:23.2:22.1:24.5:10:60:2026-04-16T14:30:00
(10 leituras, período de 60 segundos, média 23.2, mín 22.1, máx 24.5)
```

---

## 5. Formato de Timestamp

**Padrão ISO 8601:**
```
YYYY-MM-DDTHH:mm:ss

Exemplos:
2026-04-16T14:30:00  (16 abril 2026, 14:30:00)
2026-04-16T14:30:45  (com segundos)
```

---

## 6. Tratamento de Erros

### 6.1 Timeout
- **Leitura:** 5 segundos (padrão)
- **Comportamento:** Desconectar cliente
- **Log:** Registar timeout

### 6.2 Desconexão Inesperada
- **Sensor desconecta:** Gateway registra e liberta recursos
- **Gateway não responde:** Sensor aguarda timeout e desconecta
- **Servidor não responde:** Gateway registra erro e tenta novamente

### 6.3 Dados Malformados
- **Rejeitados com NACK**
- **Registados em log de erros**
- **Não interrompem conexão** (sensor continua enviando)

---

## 7. Estados da Conexão

```
SENSOR                          GATEWAY
  |                               |
  |------------ INIT -----------? |
  |                               |
  | ?----------- ACK_INIT -------- |
  |                               |
  |--- CAPABILITIES:TEMP,HUM ----? |
  |                               |
  | ?---- ACK_CAPABILITIES ------- |
  |                               |
  | ? READY PARA ENVIAR DADOS --- |
  |                               |
  |-------- DATA:TEMP:23.5 ------? | ? SERVIDOR
  |                               |
  | ?--------- ACK_DATA --------- |  ? SERVIDOR
  |                               |
  |-------- DATA:HUM:65 --------? | ? SERVIDOR
  |                               |
  | ?--------- ACK_DATA --------- |  ? SERVIDOR
  |                               |
  |---------- END ---------------? |
  |                               |
  | ?--------- ACK_END --------- |
  |                               |
```

---

## 8. Segurança e Validações

### 8.1 Comprimento Máximo de Mensagem
- **Limite:** 1024 bytes

### 8.2 Caracteres Válidos
- Apenas ASCII (0-127)
- Separador: `:` (dois-pontos)

### 8.3 Validações de Entrada
1. Verificar comprimento
2. Verificar caracteres válidos
3. Verificar formato esperado
4. Verificar valores dentro de intervalo
5. Verificar tipo previamente declarado

---

## 9. Exemplo de Sessão Completa

```
=== CONECTAR ===
SENSOR: INIT
GATEWAY: ACK_INIT

=== DECLARAR CAPACIDADES ===
SENSOR: CAPABILITIES:TEMP,HUM
GATEWAY: ACK_CAPABILITIES

=== ENVIAR DADOS ===
SENSOR: DATA:TEMP:23.5:C:GOOD
GATEWAY: ACK_DATA
  ? GATEWAY para SERVIDOR: STORE_DATA:TEMP:23.5:2026-04-16T14:30:00:GW001
  ? SERVIDOR para GATEWAY: ACK_STORE:DATA_001

SENSOR: DATA:HUM:65:PERCENT:GOOD
GATEWAY: ACK_DATA
  ? GATEWAY para SERVIDOR: STORE_DATA:HUM:65:2026-04-16T14:30:00:GW001
  ? SERVIDOR para GATEWAY: ACK_STORE:DATA_002

SENSOR: DATA:TEMP:24.0:C:GOOD
GATEWAY: ACK_DATA
  ? GATEWAY para SERVIDOR: STORE_DATA:TEMP:24.0:2026-04-16T14:30:05:GW001
  ? SERVIDOR para GATEWAY: ACK_STORE:DATA_003

=== ENCERRAR ===
SENSOR: END
GATEWAY: ACK_END
(Conexão fechada)
```

---

## 10. Estrutura de Dados (JSON)

### 10.1 Leitura de Sensor

```json
{
  "sensorId": "SENSOR_001",
  "type": "TEMP",
  "value": 23.5,
  "unit": "C",
  "quality": "GOOD",
  "timestamp": "2026-04-16T14:30:00Z",
  "gatewayId": "GW001"
}
```

### 10.2 Dados Agregados

```json
{
  "aggregationId": "AGG_001",
  "type": "TEMP",
  "period": "300",
  "statistics": {
    "count": 10,
    "average": 23.2,
    "minimum": 22.1,
    "maximum": 24.5,
    "standardDeviation": 0.8
  },
  "startTime": "2026-04-16T14:25:00Z",
  "endTime": "2026-04-16T14:30:00Z",
  "gatewayId": "GW001"
}
```

---

## Notas Finais

- Protocolo é **síncrono e orientado a texto**
- Usa **TCP para garantir entrega**
- **Sem criptografia** (pode ser adicionado later)
- **Sem autenticação** (pode ser adicionado later)
- **Case-sensitive** para comandos e tipos
