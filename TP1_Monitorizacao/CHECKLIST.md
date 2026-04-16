# Checklist de Implementação - Progresso Actual

---

## ? FASE 1: Análise e Preparação - COMPLETA

### Documentação Criada:

1. **PROTOCOLO_COMUNICACAO.md** ?
   - ? Visão geral do sistema
   - ? Fluxo de comunicação (SENSOR ? GATEWAY ? SERVIDOR)
   - ? Protocolo SENSOR ? GATEWAY com todos os comandos
   - ? Protocolo GATEWAY ? SERVIDOR
   - ? Tratamento de erros e timeouts
   - ? Máquina de estados das conexões
   - ? Exemplos completos de sessões
   - ? Estrutura JSON para dados

2. **ESTRUTURA_FICHEIROS.md** ?
   - ? Estrutura de directórios para Gateway
   - ? Estrutura de directórios para Servidor
   - ? Formato de ficheiros JSON
   - ? Validações de dados
   - ? Intervalos de valores válidos
   - ? Detecção de outliers
   - ? Validações de protocolo
   - ? Política de limpeza de dados

---

## ? FASE 2: Implementação no GATEWAY - EM PROGRESSO (60%)

### Ponto 3: Melhorar tratamento de múltiplos sensores ? COMPLETO

**Ficheiros Criados:**
- ? `Gateway/Models/SensorInfo.cs` - Classe para representar sensor
- ? `Gateway/Managers/SensorManager.cs` - Gerenciamento de sensores
- ? `Gateway/Program.cs` - Actualizado com multi-threading

**Funcionalidades:**
- ? Implementar multi-threading (uma thread por sensor)
- ? Criar classe `SensorInfo` com propriedades:
  - ? sensorId
  - ? ipAddress
  - ? port
  - ? capabilities (List<string>)
  - ? lastDataTime
  - ? connected (bool)
  - ? dataCount, errorCount
- ? Criar `SensorManager` para manter registo de sensores activos
- ? Implementar `active_sensors.json` para persistência
- ? Thread de limpeza periódica (60 minutos)
- ? Timeout de 5 segundos

**Documento:** `FASE2_PONTO3_COMPLETO.md`

---

### Ponto 4: Implementar camada de pré-processamento ? COMPLETO

**Ficheiros Criados:**
- ? `Gateway/Services/DataValidator.cs` - Validação de dados
- ? `Gateway/Services/DataPreprocessor.cs` - Pré-processamento

**Funcionalidades DataValidator:**
- ? `ValidateFormat(message)` - Valida comprimento, ASCII, estrutura
- ? `ValidateType(type)` - Valida tipo suportado
- ? `ValidateTypeInCapabilities(type, caps)` - Valida se foi declarado
- ? `ValidateValue(valueStr)` - Valida número válido
- ? `ValidateRange(type, value)` - Valida intervalo:
  - TEMP: -50 a 50 °C
  - HUM: 0 a 100 %
  - PRESS: 300 a 1100 hPa
  - LIGHT: 0 a 100000 lux
  - CO2: 0 a 5000 ppm
- ? `ValidateDataMessage()` - Valida mensagem DATA completa
- ? Métodos auxiliares: `GetDefaultUnit()`, `GetTypeDescription()`

**Funcionalidades DataPreprocessor:**
- ? Classe `SensorReading` - Leitura estruturada
- ? Classe `SensorStatistics` - Estatísticas calculadas
- ? `CreateReading()` - Cria leitura estruturada
- ? `AddToHistory()` - Adiciona ao histórico (máx 100)
- ? `GetHistory()` - Obtém histórico
- ? `CalculateStatistics()` - Calcula avg, min, max, stdDev, count
- ? `DetectOutlier()` - Detecta outliers com Z-score
- ? `DetermineQuality()` - Determina GOOD/FAIR/POOR
- ? `PreprocessReading()` - Processa completo
- ? `ClearHistory()` - Limpa histórico

**Detecção de Outliers (Z-Score):**
- ? GOOD: -2? ? valor ? 2? (95% confiança)
- ? FAIR: -3? ? valor ? 3? (99.7% confiança)
- ? POOR: valor < -3? OU valor > 3? (outlier)

**Documento:** `FASE2_PONTO4_COMPLETO.md`

---

## ?? Próximos Passos

### Ponto 5: Implementar armazenamento em ficheiros

**A fazer:**
- [ ] Criar classe `FileManager`
- [ ] Criar directório `data/raw/{DATE}/`
- [ ] Armazenar leituras em `data/raw/{DATE}/GW001_{HH}-{MM}.json`
- [ ] Implementar rotação de ficheiros (cada 15 minutos)
- [ ] Adicionar flag `processed` nas leituras
- [ ] Formato JSON estruturado

### Ponto 6: Implementar camada de agregação

**A fazer:**
- [ ] Criar classe `AggregationEngine`
- [ ] Agregar por período (5 minutos padrão)
- [ ] Calcular: count, avg, min, max, stdDev
- [ ] Armazenar em `data/aggregated/{DATE}/{TYPE}_AGG_{HH}-{MM}.json`
- [ ] Manter metadados de processamento
     - `ValidateType(type, capabilities)` ? bool
   - [ ] Classe `DataPreprocessor` com métodos:
     - `AddTimestamp(record)` ? record
     - `CheckQuality(value, sensorHistory)` ? quality
     - `DetectOutliers(value, statistics)` ? bool
   - [ ] Manter histórico de valores por tipo de sensor

5. **Implementar armazenamento em ficheiros**
   - [ ] Classe `FileManager` com métodos:
     - `CreateRawDataFile(gatewayId, timestamp)` ? fileName
     - `AppendRawRecord(fileName, record)` ? void
     - `RolloverFile()` ? void (a cada 15 minutos)
   - [ ] Armazenar em `data/raw/{DATE}/GW{ID}_{HH}-{MM}.json`
   - [ ] Implementar rotação de ficheiros (cada 15 minutos)

6. **Implementar camada de agregação**
   - [ ] Classe `AggregationEngine` com métodos:
     - `Aggregate(records, period)` ? aggregation
     - `CalculateStatistics(values)` ? statistics
     - `SaveAggregation(aggregation)` ? void
   - [ ] Agregação por período (padrão: 5 minutos)
   - [ ] Calcular: count, avg, min, max, stdDev
   - [ ] Armazenar em `data/aggregated/{DATE}/{TYPE}_AGG_{HH}-{MM}.json`

---

### FASE 3: Implementação no SERVIDOR

7. **Melhorar recepção de dados agregados**
   - [ ] Validar mensagens STORE_DATA
   - [ ] Adicionar registo estruturado
   - [ ] Implementar confirmação com DATA_ID

8. **Implementar armazenamento em ficheiros no Servidor**
   - [ ] Classe `ServerFileManager` com métodos:
     - `SaveReceivedData(gatewayId, records)` ? void
     - `CreateIndex()` ? void
   - [ ] Armazenar em `data/received/{DATE}/{HH}-{MM}_to_{HH}-{MM}.json`
   - [ ] Criar índices para busca rápida

---

### FASE 4: Integração e Comunicação

9. **Melhorar protocolo GATEWAY ? SERVIDOR**
   - [ ] Enviar dados agregados (não apenas brutos)
   - [ ] Implementar confirmação com DATA_ID
   - [ ] Tratar erros de comunicação e retry

10. **Implementar confirmação de processamento**
    - [ ] Gateway aguarda confirmação antes de descartar
    - [ ] Implementar mecanismo de retry (3x com delay exponencial)
    - [ ] Log de operações com sucesso/falha

---

### FASE 5: Feature Adicional - Base de Dados Relacional

11-14. Base de dados (se tempo permitir)

---

## ?? Recomendações de Implementação

### Ordem Sugerida:

1. **Primeiro:** Melhorar GATEWAY (pontos 3-6)
   - Adiciona mais robustez ao sistema
   - Multi-threading essencial
   - Pré-processamento melhora qualidade dos dados

2. **Segundo:** Melhorar SERVIDOR (pontos 7-8)
   - Persistência centralizada
   - Preparar para relatórios

3. **Terceiro:** Integração (pontos 9-10)
   - Confirmar protocolo funciona correctamente
   - Melhorar confiabilidade

4. **Quarto:** Base de dados (pontos 11-14)
   - Feature adicional (mais pontos)
   - Melhora buscas e relatórios

---

## ?? Diagrama de Arquitectura

```
???????????????????????????????????????????????????????????????
?                      SISTEMA DISTRIBUÍDO                   ?
???????????????????????????????????????????????????????????????

????????????????????????????????????????????????????????????????
? SENSOR                          GATEWAY                      ?
? ??????????????????????       ??????????????????????????????? ?
? ? • Medir dados      ?   ????? • ThreadPool (multi-sensor) ? ?
? ? • INIT             ?       ? • DataValidator             ? ?
? ? • CAPABILITIES     ?????   ? • DataPreprocessor          ? ?
? ? • DATA             ?   ?   ? • AggregationEngine         ? ?
? ? • END              ?   ?   ? • FileManager (raw)         ? ?
? ?                    ?   ?   ? • FileManager (aggregated)  ? ?
? ? (Múltiplos sensores?   ?   ? • SensorManager             ? ?
? ?  podem conectar)   ?   ?   ? • Logger                    ? ?
? ??????????????????????   ?   ??????????????????????????????? ?
?                          ?            ?                      ?
?                          ?    ??????????????????????          ?
?                          ?    ?                    ?          ?
?                          ?    ? data/raw/          ?          ?
?                          ?    ? data/aggregated/   ?          ?
?                          ?    ? logs/              ?          ?
?                          ?    ? cache/             ?          ?
?                          ?    ??????????????????????          ?
????????????????????????????????????????????????????????????????
                            ?
                            ? STORE_DATA
                            ?
          ??????????????????????????????????????
          ?         SERVIDOR (5002)            ?
          ? ?????????????????????????????????  ?
          ? ? • TcpListener                 ?  ?
          ? ? • DataReceiver                ?  ?
          ? ? • ServerFileManager           ?  ?
          ? ? • IndexEngine                 ?  ?
          ? ? • Logger                      ?  ?
          ? ?????????????????????????????????  ?
          ?            ?                       ?
          ?    ??????????????????????          ?
          ?    ?                    ?          ?
          ?    ? data/received/     ?          ?
          ?    ? data/reports/      ?          ?
          ?    ? index/             ?          ?
          ?    ? logs/              ?          ?
          ?    ??????????????????????          ?
          ??????????????????????????????????????
```

---

## ?? Critérios de Sucesso para FASE 1

? **Documentação completa do protocolo**
- Todos os comandos especificados
- Exemplos de mensagens
- Tratamento de erros definido

? **Estrutura de ficheiros definida**
- Directórios criados
- Formato JSON especificado
- Validações documentadas

? **Pronto para implementação**
- Equipa compreende o protocolo
- Ficheiros de especificação criados
- Próximos passos claros

---

**Status:** ? FASE 1 CONCLUÍDA
**Data:** 16 de Abril de 2026
**Documento:** PROTOCOLO_COMUNICACAO.md + ESTRUTURA_FICHEIROS.md + CHECKLIST.md
