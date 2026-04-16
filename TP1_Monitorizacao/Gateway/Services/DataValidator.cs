using System;
using System.Collections.Generic;
using System.Linq;

namespace Gateway.Services
{
    /// <summary>
    /// Valida dados recebidos de sensores
    /// </summary>
    public class DataValidator
    {
        /// <summary>
        /// Valida o formato de uma mensagem
        /// </summary>
        public bool ValidateFormat(string message, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(message))
            {
                error = "Mensagem vazia";
                return false;
            }

            if (message.Length > 1024)
            {
                error = "Mensagem excede 1024 bytes";
                return false;
            }

            // Validar caracteres ASCII
            if (!message.All(c => c >= 0 && c <= 127))
            {
                error = "Mensagem contém caracteres não-ASCII";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Valida se um tipo de dado é suportado
        /// </summary>
        public bool ValidateType(string type, out string error)
        {
            error = null;

            var supportedTypes = new[] { "TEMP", "HUM", "PRESS", "LIGHT", "CO2" };
            if (!supportedTypes.Contains(type))
            {
                error = $"Tipo '{type}' não suportado";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Valida se um tipo foi declarado nas capabilities
        /// </summary>
        public bool ValidateTypeInCapabilities(string type, List<string> capabilities, out string error)
        {
            error = null;

            if (capabilities == null || !capabilities.Contains(type))
            {
                error = $"Tipo '{type}' não foi declarado em CAPABILITIES";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Valida se o valor é um número válido
        /// </summary>
        public bool ValidateValue(string valueStr, out double value, out string error)
        {
            error = null;
            value = 0;

            if (string.IsNullOrWhiteSpace(valueStr))
            {
                error = "Valor vazio";
                return false;
            }

            if (!double.TryParse(valueStr, out value))
            {
                error = $"Valor '{valueStr}' não é um número válido";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Valida se o valor está dentro do intervalo permitido para o tipo
        /// </summary>
        public bool ValidateRange(string type, double value, out string error)
        {
            error = null;

            var (min, max) = GetRangeForType(type);
            
            if (value < min || value > max)
            {
                error = $"Valor {value} fora do intervalo [{min}, {max}] para tipo {type}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Obtém intervalo válido para um tipo de dado
        /// </summary>
        private (double min, double max) GetRangeForType(string type)
        {
            return type switch
            {
                "TEMP" => (-50, 50),
                "HUM" => (0, 100),
                "PRESS" => (300, 1100),
                "LIGHT" => (0, 100000),
                "CO2" => (0, 5000),
                _ => (double.MinValue, double.MaxValue)
            };
        }

        /// <summary>
        /// Valida uma mensagem DATA completa
        /// </summary>
        public bool ValidateDataMessage(string message, List<string> capabilities, out string type, out double value, out string error)
        {
            type = null;
            value = 0;
            error = null;

            // Formato esperado: DATA:TYPE:VALUE[:UNIT][:QUALITY]
            if (!message.StartsWith("DATA:"))
            {
                error = "Mensagem não começa com DATA:";
                return false;
            }

            var parts = message.Split(':');
            if (parts.Length < 3)
            {
                error = "Formato DATA inválido (esperado: DATA:TYPE:VALUE)";
                return false;
            }

            type = parts[1];
            string valueStr = parts[2];

            // Validar tipo
            if (!ValidateType(type, out error))
                return false;

            // Validar se tipo foi declarado
            if (!ValidateTypeInCapabilities(type, capabilities, out error))
                return false;

            // Validar valor
            if (!ValidateValue(valueStr, out value, out error))
                return false;

            // Validar intervalo
            if (!ValidateRange(type, value, out error))
                return false;

            return true;
        }

        /// <summary>
        /// Obtém unidade padrão para um tipo
        /// </summary>
        public string GetDefaultUnit(string type)
        {
            return type switch
            {
                "TEMP" => "C",
                "HUM" => "%",
                "PRESS" => "hPa",
                "LIGHT" => "lux",
                "CO2" => "ppm",
                _ => ""
            };
        }

        /// <summary>
        /// Obtém descrição padrão para um tipo
        /// </summary>
        public string GetTypeDescription(string type)
        {
            return type switch
            {
                "TEMP" => "Temperatura Ambiente",
                "HUM" => "Humidade Relativa",
                "PRESS" => "Pressão Atmosférica",
                "LIGHT" => "Luminosidade",
                "CO2" => "Dióxido de Carbono",
                _ => "Tipo Desconhecido"
            };
        }
    }
}
