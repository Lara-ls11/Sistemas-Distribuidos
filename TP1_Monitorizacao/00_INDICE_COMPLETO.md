# ?? Índice Completo - Implementação da Funcionalidade SENSOR

## ?? Status Geral

```
FASE 1: Documentação              ???????????????????? 100% ?
FASE 2: Gateway (Ponto 3-4)       ????????????????????  60% ??
FASE 2: Gateway (Ponto 5-6)       ????????????????????   0% ?

Total Progresso: 40% do Projeto
```

---

## ?? Documentos de Referência

### ?? FASE 1: Análise e Preparação

| Documento | Tamanho | Status | Utilidade |
|-----------|---------|--------|-----------|
| **PROTOCOLO_COMUNICACAO.md** | 7.5 KB | ? | Especificação completa do protocolo TCP |
| **ESTRUTURA_FICHEIROS.md** | 9.3 KB | ? | Layout de directórios e ficheiros JSON |
| **CHECKLIST.md** | 9.7 KB | ? | Status de todos os 6 pontos |

**Lê isto primeiro:**
? PROTOCOLO_COMUNICACAO.md (para entender o fluxo)
? ESTRUTURA_FICHEIROS.md (para entender os dados)

---

### ?? FASE 2: Implementação no GATEWAY

| Documento | Tamanho | Status | Contenção |
|-----------|---------|--------|-----------|
| **FASE2_PONTO3_COMPLETO.md** | 8.3 KB | ? | Multi-threading + SensorManager |
| **FASE2_PONTO4_COMPLETO.md** | 8.7 KB | ? | Validação + Pré-processamento |
| **PROXIMO_PASSO_PONTO5.md** | 7.4 KB | ?? | Armazenamento em ficheiros |

**Lê isto:**
? FASE2_PONTO3_COMPLETO.md (ponto 3 completo)
? FASE2_PONTO4_COMPLETO.md (ponto 4 completo)
? PROXIMO_PASSO_PONTO5.md (instruções ponto 5)

---

### ?? Resumos Executivos

| Documento | Tamanho | Tipo | Leitura |
|-----------|---------|------|---------|
| **RESUMO_EXECUTIVO.md** | 9.6 KB | Visão Geral | 5-10 min |
| **RESUMO_PROGRESSO.md** | 6.8 KB | Status | 5 min |

**Lê isto para:**
? RESUMO_EXECUTIVO.md (antes de começar novo ponto)
? RESUMO_PROGRESSO.md (verificar progresso geral)

---

## ??? Código Implementado

### Gateway - Estrutura de Ficheiros

```
Gateway/
??? Models/
?   ??? SensorInfo.cs              (58 linhas)   ?
?       Classe: SensorInfo
?
??? Managers/
?   ??? SensorManager.cs           (212 linhas)  ?
?       Classe: SensorManager
?
??? Services/
?   ??? DataValidator.cs           (170 linhas)  ?
?   ?   Classe: DataValidator
?   ?
?   ??? DataPreprocessor.cs        (280 linhas)  ?
?       Classes: SensorReading
?                SensorStatistics
?                DataPreprocessor
?
??? Program.cs                     (240 linhas)  ? ACTUALIZADO
    Método: Main()
    Método: HandleSensor()
    Método: ProcessMessage()
    Método: ValidateDataRange()
    Método: SendToServer()
    Método: CleanupThread()
```

### Sensor / Servidor (Não alterados)

```
Sensor/
??? Program.cs                     (Original + Funcional)

Servidor/
??? Program.cs                     (Original + Funcional)
```

---

## ?? Estatísticas de Código

### Linhas de Código por Componente

```
Documentação:        ~2,000 linhas
Gateway.Models:         58 linhas
Gateway.Managers:       212 linhas
Gateway.Services:       450 linhas
Gateway.Program:        240 linhas
????????????????????????????????
TOTAL:              2,960 linhas

Compilação: ? Sucesso (19 avisos não-críticos)
```

### Complexidade por Ficheiro

| Ficheiro | Métodos | Classes | Linhas | Complexidade |
|----------|---------|---------|--------|-------------|
| SensorInfo.cs | 3 | 1 | 58 | Baixa |
| SensorManager.cs | 12 | 1 | 212 | Média |
| DataValidator.cs | 8 | 1 | 170 | Média |
| DataPreprocessor.cs | 10 | 3 | 280 | Alta |
| Program.cs | 8 | 1 | 240 | Alta |

---

## ?? Funcionalidades Implementadas

### ? Ponto 3: Multi-threading (100%)
- [x] Threading com Task.Run()
- [x] SensorInfo para representar sensor
- [x] SensorManager thread-safe
- [x] Persistência em JSON
- [x] IDs únicos automáticos
- [x] Limpeza de inativos

### ? Ponto 4: Validação e Pré-processamento (100%)
- [x] DataValidator com 7 camadas
- [x] DataPreprocessor com estatísticas
- [x] Histórico de 100 leituras
- [x] Detecção de outliers (Z-score)
- [x] Qualidade automática (GOOD/FAIR/POOR)
- [x] Logging estruturado

### ? Ponto 5: Armazenamento em Ficheiros (0%)
- [ ] FileManager para persistência
- [ ] Directório data/raw/{DATE}/
- [ ] Ficheiros GW001_{HH}-{MM}.json
- [ ] Rotação de ficheiros (15 min)
- [ ] Limpeza automática (7 dias)

### ? Ponto 6: Agregação de Dados (0%)
- [ ] AggregationEngine
- [ ] Período de agregação (5 min)
- [ ] Directório data/aggregated/
- [ ] Cálculo de estatísticas
- [ ] Envio para SERVIDOR

### ? Ponto 7-10: Servidor e BD (0%)
- [ ] Melhorias no Servidor
- [ ] Persistência de dados
- [ ] Base de dados (opcional)

---

## ?? Como Começar

### 1. Leitura de Introdução (15 min)
```
Lê em ordem:
1. PROTOCOLO_COMUNICACAO.md  ? Entender protocolo
2. ESTRUTURA_FICHEIROS.md     ? Entender dados
3. RESUMO_EXECUTIVO.md        ? Visão geral implementação
```

### 2. Entender o Código Actual (30 min)
```
Explora:
- Gateway/Models/SensorInfo.cs      ? Estrutura sensor
- Gateway/Managers/SensorManager.cs ? Gerenciamento
- Gateway/Services/DataValidator.cs ? Validação
- Gateway/Services/DataPreprocessor.cs ? Processamento
- Gateway/Program.cs                ? Fluxo principal
```

### 3. Compilar e Testar (20 min)
```bash
cd TP1_Monitorizacao
dotnet build              # Compilar tudo

# Terminal 1: Servidor
cd Servidor && dotnet run

# Terminal 2: Gateway
cd Gateway && dotnet run

# Terminal 3: Sensor
cd Sensor && dotnet run
```

### 4. Próximo Passo (60 min)
```
Implementar Ponto 5:
- Lê PROXIMO_PASSO_PONTO5.md
- Cria Gateway/Services/FileManager.cs
- Integra no Gateway/Program.cs
- Testa com sensores múltiplos
```

---

## ?? Quick Reference

### Protocolo Básico (Sensor ? Gateway)
```
INIT                          ? ACK_INIT
CAPABILITIES:TEMP,HUM         ? ACK_CAPABILITIES
DATA:TEMP:23.5:C:GOOD        ? ACK_DATA
END                           ? ACK_END
```

### Tipos de Dados Suportados
```
TEMP   ? Temperatura (-50 a 50 °C)
HUM    ? Humidade (0 a 100 %)
PRESS  ? Pressão (300 a 1100 hPa)
LIGHT  ? Luminosidade (0 a 100000 lux)
CO2    ? Dióxido de Carbono (0 a 5000 ppm)
```

### Qualidade de Dados (Z-Score)
```
GOOD ? Normal (95% confiança)
FAIR ? Possível anomalia (99.7% confiança)
POOR ? Outlier (rejeitar)
```

### Directórios Criados
```
cache/                          # Sensores activos
  ??? active_sensors.json

data/                           # Dados (a criar)
  ??? raw/
  ?   ??? {DATE}/
  ?       ??? GW001_HH-MM.json
  ??? aggregated/
  ?   ??? {DATE}/
  ?       ??? {TYPE}_AGG_HH-MM.json
  ??? logs/
      ??? *.log
```

---

## ?? Progresso Timeline

```
16 Abril 2026
?? 09:00 - Fase 1 Completa (Documentação) ?
?? 10:30 - Ponto 3 Completo (Multi-threading) ?
?? 12:00 - Ponto 4 Completo (Validação) ?
?
?? 13:00 - Próximo: Ponto 5 (Ficheiros) ?
?? 14:00 - Próximo: Ponto 6 (Agregação) ?
?
?? 15:00 - FASE 2 Completa! ?? (Estimado)
```

---

## ?? Suporte Rápido

### Precisa de...

**Entender o protocolo?**
? Lê PROTOCOLO_COMUNICACAO.md

**Saber como dados são validados?**
? Lê FASE2_PONTO4_COMPLETO.md

**Entender multi-threading?**
? Lê FASE2_PONTO3_COMPLETO.md

**Ver o que fazer a seguir?**
? Lê PROXIMO_PASSO_PONTO5.md

**Ver status geral?**
? Lê RESUMO_EXECUTIVO.md

**Verificar compilação?**
```bash
cd TP1_Monitorizacao && dotnet build
```

**Rodar exemplo?**
? Seguir instruções em RESUMO_PROGRESSO.md

---

## ?? Tecnologias Utilizadas

```
? C# 10
? .NET 10.0
? TCP Sockets
? Threading (Task.Run, ConcurrentDictionary)
? JSON Serialization (System.Text.Json)
? LINQ (Queries sobre colecções)
? Estatística (Z-score, desvio padrão)
```

---

## ?? Checklist para Próximo Ponto

```
Antes de começar Ponto 5:

? Lê PROXIMO_PASSO_PONTO5.md completamente
? Entende estrutura de FileManager
? Planeia integração com Program.cs
? Prepara testes
? Cria ficheiro Gateway/Services/FileManager.cs
? Implementa métodos principais
? Compila sem erros
? Testa com sensores múltiplos
? Verifica ficheiros criados em data/raw/
? Documenta em FASE2_PONTO5_COMPLETO.md
```

---

**Última Atualização:** 16 de Abril de 2026
**Versão:** 2.0 (Pontos 3-4 Completos)
**Próxima:** Ponto 5 (Armazenamento em Ficheiros)
**Tempo Total Investido:** ~2 horas
**Tempo Estimado para Conclusão:** ~1 hora mais

---

## ?? Notas Finais

- ? Toda a FASE 1 completa com documentação detalhada
- ? Pontos 3 e 4 implementados e compilados
- ? Gateway suporta múltiplos sensores
- ? Validação em 7 camadas implementada
- ? Pré-processamento com estatísticas funcionando
- ? Próxima: Persistência de dados em ficheiros
- ?? Objectivo: Completar FASE 2 até final do dia

**Status: PRONTO PARA PONTO 5 ?**
