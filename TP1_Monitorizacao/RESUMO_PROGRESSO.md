# ?? Resumo do Progresso - FASE 2

## Status Actual: 60% COMPLETO (4 de 6 pontos da Fase 2)

```
FASE 1: Análise e Preparação
???????????????????????????? 100% ?

FASE 2: Implementação no GATEWAY  
????????????????????????????  60% ??

FASE 3: Implementação no SERVIDOR
????????????????????????????   0% ?

FASE 4: Integração e Comunicação
????????????????????????????   0% ?

FASE 5: Base de Dados (Opcional)
????????????????????????????   0% ?
```

---

## ?? Ficheiros Criados até Agora

### Documentação (3 ficheiros)
```
? PROTOCOLO_COMUNICACAO.md      (262 linhas)
? ESTRUTURA_FICHEIROS.md        (340 linhas)
? CHECKLIST.md                  (180 linhas actualizado)
```

### Gateway - Núcleo do Sistema (5 ficheiros)

**Modelos:**
```
? Gateway/Models/SensorInfo.cs           (58 linhas)
   - Representa um sensor conectado
   - Propriedades: ID, IP, capabilities, conectado, etc.
   - Serialização JSON
```

**Gerenciamento:**
```
? Gateway/Managers/SensorManager.cs      (212 linhas)
   - Gerencia sensores activos
   - Thread-safe (ConcurrentDictionary)
   - Persistência em JSON
   - Limpeza automática
```

**Validação:**
```
? Gateway/Services/DataValidator.cs      (170 linhas)
   - Valida formato, tipo, valor, intervalo
   - Mensagens de erro estruturadas
   - Métodos auxiliares
```

**Pré-processamento:**
```
? Gateway/Services/DataPreprocessor.cs   (280 linhas)
   - Classe SensorReading
   - Classe SensorStatistics
   - Histórico de valores (máx 100)
   - Detecção de outliers (Z-score)
   - Qualidade: GOOD/FAIR/POOR
```

**Programa Principal:**
```
? Gateway/Program.cs                    (240 linhas actualizado)
   - Multi-threading
   - HandleSensor por thread
   - Validação de dados
   - Envio para servidor
   - Thread de limpeza periódica
```

---

## ?? Funcionalidades Implementadas

### ? Multi-threading
- Gateway aceita múltiplos sensores simultaneamente
- Cada sensor processado em thread separada (Task.Run)
- Thread-safe com locks onde necessário

### ? Gerenciamento de Sensores
- Registo automático de sensores
- Atribuição de IDs únicos (SENSOR_001, SENSOR_002, ...)
- Rastreio de último tempo de leitura
- Contador de dados recebidos e erros
- Limpeza de sensores inativos

### ? Validação de Dados
- Formato da mensagem
- Tipo de dados (TEMP, HUM, PRESS, LIGHT, CO2)
- Valor numérico válido
- Intervalo permitido por tipo
- Tipo foi declarado em CAPABILITIES

### ? Pré-processamento
- Histórico de até 100 leituras por sensor-tipo
- Estatísticas: média, min, max, desvio padrão, soma
- Detecção de outliers usando Z-score
- Qualidade automática: GOOD (95%), FAIR (99.7%), POOR (outlier)
- Combinação de qualidade (sensor + detectada)

### ? Logging Estruturado
- [INFO] - Eventos importantes
- [DEBUG] - Mensagens de protocolo
- [ERROR] - Erros e exceções
- [WARNING] - Possíveis problemas

### ? Persistência
- Ficheiro `cache/active_sensors.json` actualizado automaticamente
- JSON bem estruturado com indentação
- Carregamento automático ao iniciar

---

## ?? Estatísticas do Código

| Componente | Ficheiros | Linhas | Status |
|------------|-----------|--------|--------|
| Documentação | 3 | 782 | ? Completo |
| Modelos | 1 | 58 | ? Completo |
| Managers | 1 | 212 | ? Completo |
| Services | 2 | 450 | ? Completo |
| Program | 1 | 240 | ? Actualizado |
| **TOTAL** | **8** | **1,742** | **? 60%** |

---

## ?? Tecnologias Usadas

```csharp
// Threading
using System.Threading;
using System.Threading.Tasks;

// Estruturas de dados thread-safe
using System.Collections.Concurrent;

// JSON Serialization
using System.Text.Json;
using System.Text.Json.Serialization;

// Operações de ficheiros
using System.IO;

// Funcionalidades LINQ
using System.Linq;
```

---

## ?? Próximos 40% (Pontos 5-6)

### Ponto 5: Armazenamento em Ficheiros
```
Ficheiros a criar:
- FileManager.cs (300-400 linhas)

Funcionalidades:
- Directório data/raw/{DATE}/
- Ficheiros GW001_{HH}-{MM}.json
- Rotação de ficheiros (15 minutos)
- Leitura/escrita JSON

Estimado: 400 linhas
```

### Ponto 6: Agregação de Dados
```
Ficheiros a criar:
- AggregationEngine.cs (250-350 linhas)

Funcionalidades:
- Período de agregação (5 min padrão)
- Cálculo de estatísticas
- Ficheiros data/aggregated/{DATE}/
- Armazenamento de agregações

Estimado: 350 linhas
```

---

## ?? Como Testar o que foi Feito

### Pré-requisitos
```bash
cd TP1_Monitorizacao
dotnet build  # Deve compilar com sucesso
```

### Terminal 1 - Servidor
```bash
cd TP1_Monitorizacao/Servidor
dotnet run
# Esperado: "Servidor à espera de dados..."
```

### Terminal 2 - Gateway
```bash
cd TP1_Monitorizacao/Gateway
dotnet run
# Esperado:
# [INFO] Gateway iniciado na porta 5001
# [INFO] À espera de sensores...
```

### Terminal 3 - Sensor 1
```bash
cd TP1_Monitorizacao/Sensor
dotnet run
# Esperado: Conexão bem-sucedida, dados enviados
```

### Terminal 4 - Sensor 2 (novo)
```bash
cd TP1_Monitorizacao/Sensor
dotnet run 127.0.0.1
# Esperado: Gateway aceita segundo sensor simultaneamente
```

### Verificar ficheiro de cache
```bash
cat TP1_Monitorizacao/Gateway/cache/active_sensors.json
# Esperado: Lista de SENSOR_001, SENSOR_002, etc.
```

---

## ?? Critérios de Sucesso Atingidos

- ? Gateway suporta múltiplos sensores (multi-threading)
- ? Cada sensor em thread separada
- ? Validação completa de dados
- ? Pré-processamento com estatísticas
- ? Detecção de outliers (Z-score)
- ? Qualidade automática de dados
- ? Logging estruturado
- ? Persistência de sensores activos
- ? Timeout de leitura (5 segundos)
- ? Limpeza periódica
- ? Compilação bem-sucedida
- ? Documentação completa

---

## ?? Documentos de Referência

```
Lê primeiro:
1. PROTOCOLO_COMUNICACAO.md     - Entender protocolo
2. ESTRUTURA_FICHEIROS.md       - Estrutura de dados

Depois:
3. FASE2_PONTO3_COMPLETO.md     - Multi-threading
4. FASE2_PONTO4_COMPLETO.md     - Pré-processamento

Monitorar progresso:
5. CHECKLIST.md                 - Status de pontos
6. RESUMO_PROGRESSO.md          - Este ficheiro
```

---

## ?? Aprendizados Chave

1. **Multi-threading em C#**
   - Task.Run para paralelismo
   - ConcurrentDictionary para thread-safety
   - Locks para operações críticas

2. **Validação de Dados**
   - Abordagem em camadas
   - Mensagens de erro específicas
   - Recuperação de erros

3. **Estatística Aplicada**
   - Z-score para outliers
   - Desvio padrão
   - Qualidade baseada em probabilidade

4. **Arquitetura de Software**
   - Separação de responsabilidades
   - Injeção de dependências
   - Padrão Manager-Service

---

**Data:** 16 de Abril de 2026
**Tempo Decorrido:** ~2 horas
**Próxima Sessão:** Pontos 5-6 (Armazenamento e Agregação)
**Tempo Estimado:** 1.5-2 horas
