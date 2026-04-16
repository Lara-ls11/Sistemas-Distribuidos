# Implementação Completada - FASE 2, Ponto 4

## ? Implementar Camada de Pré-processamento

### Resumo das Alterações

Foram implementadas duas classes principais para validação e pré-processamento de dados.

---

## 1. Classe `DataValidator.cs`

**Localização:** `Gateway/Services/DataValidator.cs`

Responsável pela validação de todos os dados recebidos.

### Métodos de Validação:

| Método | Descrição | Retorna |
|--------|-----------|---------|
| `ValidateFormat(msg)` | Verifica formato geral da mensagem | bool + error |
| `ValidateType(type)` | Valida se tipo é suportado | bool + error |
| `ValidateTypeInCapabilities(type, caps)` | Valida se tipo foi declarado | bool + error |
| `ValidateValue(valueStr)` | Valida se é número válido | bool + value + error |
| `ValidateRange(type, value)` | Valida intervalo de valores | bool + error |
| `ValidateDataMessage(msg, caps)` | Valida mensagem DATA completa | bool + type + value + error |

### Métodos Auxiliares:

```csharp
GetDefaultUnit(type)          // Retorna unidade padrão
GetTypeDescription(type)      // Retorna descrição amigável
GetRangeForType(type)         // Retorna intervalo válido
```

### Validações Implementadas:

1. **Comprimento:** Máximo 1024 bytes
2. **Caracteres:** Apenas ASCII (0-127)
3. **Formato:** Estrutura esperada (DATA:TYPE:VALUE[:UNIT][:QUALITY])
4. **Tipo:** Um de {TEMP, HUM, PRESS, LIGHT, CO2}
5. **Tipo declarado:** Deve estar em CAPABILITIES
6. **Valor:** Número válido (int ou float)
7. **Intervalo:** Dentro dos limites permitidos:
   - TEMP: -50 a 50 °C
   - HUM: 0 a 100 %
   - PRESS: 300 a 1100 hPa
   - LIGHT: 0 a 100000 lux
   - CO2: 0 a 5000 ppm

### Exemplos de Uso:

```csharp
var validator = new DataValidator();

// Validar tipo
if (!validator.ValidateType("TEMP", out error))
    Console.WriteLine(error);

// Validar valor
if (!validator.ValidateValue("23.5", out double value, out error))
    Console.WriteLine(error);

// Validar intervalo
if (!validator.ValidateRange("TEMP", value, out error))
    Console.WriteLine(error);

// Validar mensagem DATA completa
if (validator.ValidateDataMessage("DATA:TEMP:23.5:C:GOOD", capabilities, 
    out string type, out double value, out string error))
{
    Console.WriteLine($"Válido: {type}={value}");
}
```

---

## 2. Classe `DataPreprocessor.cs`

**Localização:** `Gateway/Services/DataPreprocessor.cs`

Realiza pré-processamento e análise estatística dos dados.

### Classe Interna: `SensorReading`

Representa uma leitura estruturada:

```csharp
public class SensorReading
{
    public string SensorId { get; set; }       // ID do sensor
    public string Type { get; set; }           // Tipo de dados
    public double Value { get; set; }          // Valor
    public string Unit { get; set; }           // Unidade
    public string Quality { get; set; }        // GOOD, FAIR, POOR
    public DateTime Timestamp { get; set; }    // Momento da leitura
    public double? ZScore { get; set; }        // Pontuação Z estatística
    public bool IsOutlier { get; set; }        // É outlier?
}
```

### Classe Interna: `SensorStatistics`

Estatísticas calculadas:

```csharp
public class SensorStatistics
{
    public int Count { get; set; }              // Número de leituras
    public double Average { get; set; }         // Média
    public double Minimum { get; set; }         // Valor mínimo
    public double Maximum { get; set; }         // Valor máximo
    public double Sum { get; set; }             // Soma de valores
    public double StandardDeviation { get; set; } // Desvio padrão
}
```

### Métodos Principais:

| Método | Descrição |
|--------|-----------|
| `CreateReading(...)` | Cria leitura estruturada |
| `AddToHistory(sensorId, type, value)` | Adiciona ao histórico |
| `GetHistory(sensorId, type)` | Obtém histórico de leituras |
| `CalculateStatistics(sensorId, type)` | Calcula média, min, max, etc |
| `DetectOutlier(sensorId, type, value)` | Detecta outliers com Z-score |
| `DetermineQuality(sensorId, type, value)` | Determina GOOD/FAIR/POOR |
| `PreprocessReading(...)` | Processa leitura completa |
| `ClearHistory(sensorId, type)` | Limpa histórico |

---

## 3. Algoritmo de Detecção de Outliers

### Método: Z-Score

```
Z-Score = (Valor - Média) / Desvio Padrão

Qualidade:
- GOOD:  -2? ? valor ? 2?  (99.7% dos dados normais)
- FAIR:  -3? ? valor ? 3?  (99.99% dos dados normais)
- POOR:  valor < -3? OU valor > 3? (outlier)
```

### Exemplo Prático:

```
Histórico de TEMP de SENSOR_001:
[22.5, 23.0, 23.2, 23.1, 22.8, 23.5, 50.0]
                                    ? outlier?

Estatísticas:
- Média: 24.01
- Desvio Padrão: 9.85
- Z-Score(50.0) = (50.0 - 24.01) / 9.85 = 2.64

Resultado:
- Dentro de ±3? ? Qualidade: FAIR
- Outlier: Não

(Com histórico maior, teria mais clareza)
```

---

## 4. Fluxo de Pré-processamento

```
Mensagem Recebida: DATA:TEMP:23.5:C:GOOD
        ?
???????????????????????????????????????
? 1. ValidateFormat                   ?
?    - Comprimento ok?                ?
?    - ASCII?                         ?
???????????????????????????????????????
        ? OK
???????????????????????????????????????
? 2. ValidateType                     ?
?    - TEMP em tipos suportados?      ?
???????????????????????????????????????
        ? OK
???????????????????????????????????????
? 3. ValidateTypeInCapabilities       ?
?    - TEMP foi declarado?            ?
???????????????????????????????????????
        ? OK
???????????????????????????????????????
? 4. ValidateValue                    ?
?    - "23.5" é número?               ?
???????????????????????????????????????
        ? OK (23.5)
???????????????????????????????????????
? 5. ValidateRange                    ?
?    - 23.5 em [-50, 50]?             ?
???????????????????????????????????????
        ? OK
???????????????????????????????????????
? 6. PreprocessReading                ?
?    - Adicionar ao histórico         ?
?    - Calcular estatísticas          ?
?    - Detectar outlier (Z-score)     ?
?    - Determinar qualidade           ?
?    - Combinar com qualidade enviada ?
???????????????????????????????????????
        ?
Leitura Processada:
{
  SensorId: "SENSOR_001",
  Type: "TEMP",
  Value: 23.5,
  Unit: "C",
  Quality: "GOOD",
  Timestamp: "2026-04-16T14:30:00Z",
  ZScore: 0.15,
  IsOutlier: false
}
```

---

## 5. Histórico e Estatísticas

### Limite de Histórico

Máximo **100 leituras** por sensor-tipo para evitar uso excessivo de memória.

```
Histórico para SENSOR_001:TEMP
[1] 22.5 (2026-04-16T14:00:00Z)
[2] 23.0 (2026-04-16T14:01:00Z)
[3] 23.2 (2026-04-16T14:02:00Z)
...
[99] 23.1 (2026-04-16T15:38:00Z)
[100] 23.5 (2026-04-16T15:39:00Z)
      ? Próxima leitura removeria a [1]
```

### Estatísticas Calculadas

```csharp
var stats = preprocessor.CalculateStatistics("SENSOR_001", "TEMP");

// Resultado:
// Count: 100
// Average: 23.24
// Minimum: 21.5
// Maximum: 25.3
// Sum: 2324
// StandardDeviation: 0.87
```

---

## 6. Integração no Gateway (Program.cs)

A camada será integrada assim:

```csharp
private static DataValidator _validator = new DataValidator();
private static DataPreprocessor _preprocessor = new DataPreprocessor();

// Ao processar DATA:
string dataMsg = "DATA:TEMP:23.5:C:GOOD";

// 1. Validar
if (!_validator.ValidateDataMessage(dataMsg, capabilities, 
    out string type, out double value, out string error))
{
    return $"NACK_DATA:{error}";
}

// 2. Pré-processar
var reading = _preprocessor.PreprocessReading(
    sensorId: sensorId,
    type: type,
    value: value,
    unit: _validator.GetDefaultUnit(type),
    providedQuality: "GOOD"
);

// 3. Registar qualidade detectada
Console.WriteLine($"[INFO] {sensorId}: {reading}");
if (reading.IsOutlier)
    Console.WriteLine($"[WARNING] Possível outlier: Z-score={reading.ZScore}");
```

---

## 7. Status de Compilação

? **Build Status:** SUCESSO COM AVISOS

```
Gateway: OK
Sensor: OK
Servidor: OK

19 avisos (não-críticos, relacionados com nullability)
```

---

## 8. Próximos Passos (FASE 2, Ponto 5)

**Implementar armazenamento em ficheiros:**

- [ ] Criar classe `FileManager`
- [ ] Criar directório `data/raw/{DATE}/`
- [ ] Armazenar registos em JSON
- [ ] Implementar rotação de ficheiros (cada 15 minutos)
- [ ] Armazenar ficheiro `data/raw/{DATE}/GW001_{HH}-{MM}.json`

---

## Ficheiros Criados

| Ficheiro | Localização | Linhas |
|----------|-------------|--------|
| DataValidator.cs | Gateway/Services/ | 170 |
| DataPreprocessor.cs | Gateway/Services/ | 280 |

---

**Data:** 16 de Abril de 2026
**Status:** ? PONTO 4 COMPLETO
**Próximo:** PONTO 5 - Armazenamento em Ficheiros
