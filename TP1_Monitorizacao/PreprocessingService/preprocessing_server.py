"""
Serviço de Pré-processamento RPC (gRPC)
Porta: 50051
Responsável por normalizar dados de sensores antes da agregação no Gateway.
"""

import grpc
import json
import math
import xml.etree.ElementTree as ET
import csv
import io
import logging
import time
from concurrent import futures
from datetime import datetime, timezone
from collections import deque

import preprocessing_pb2
import preprocessing_pb2_grpc

logging.basicConfig(
    level=logging.INFO,
    format='[%(levelname)s] %(asctime)s - %(message)s',
    datefmt='%H:%M:%S'
)
log = logging.getLogger(__name__)

# ── Configuração de intervalos válidos por tipo de sensor ────────────────────
SENSOR_RANGES = {
    'TEMP':     {'min': -40.0,  'max': 85.0,   'unit': '°C'},
    'HUM':      {'min': 0.0,    'max': 100.0,  'unit': '%'},
    'PM25':     {'min': 0.0,    'max': 1000.0, 'unit': 'µg/m³'},
    'NO2':      {'min': 0.0,    'max': 2000.0, 'unit': 'µg/m³'},
    'ACOUSTIC': {'min': 20.0,   'max': 140.0,  'unit': 'dB'},
    'CO':       {'min': 0.0,    'max': 100.0,  'unit': 'ppm'},
    'O3':       {'min': 0.0,    'max': 500.0,  'unit': 'µg/m³'},
}

# ── Histórico por sensor/tipo para deteção de outliers (Z-score) ─────────────
_history: dict[str, deque] = {}
MAX_HISTORY = 100


def _get_history(sensor_id: str, data_type: str) -> deque:
    key = f"{sensor_id}:{data_type}"
    if key not in _history:
        _history[key] = deque(maxlen=MAX_HISTORY)
    return _history[key]


def _compute_zscore(hist: deque, value: float) -> float:
    if len(hist) < 2:
        return 0.0
    mean = sum(hist) / len(hist)
    variance = sum((x - mean) ** 2 for x in hist) / len(hist)
    std = math.sqrt(variance)
    return (value - mean) / std if std > 0 else 0.0


def _determine_quality(z: float, value: float, data_type: str) -> str:
    """GOOD / FAIR / POOR baseado em Z-score e limites físicos."""
    r = SENSOR_RANGES.get(data_type)
    if r and (value < r['min'] or value > r['max']):
        return 'POOR'
    if abs(z) > 3.0:
        return 'POOR'
    if abs(z) > 2.0:
        return 'FAIR'
    return 'GOOD'


def _get_unit(data_type: str, provided_unit: str) -> str:
    """Retorna unidade canónica para o tipo de dado."""
    if provided_unit:
        return provided_unit
    r = SENSOR_RANGES.get(data_type.upper())
    return r['unit'] if r else ''


# ── Conversão de formatos ─────────────────────────────────────────────────────

def _from_xml(raw: str) -> dict:
    root = ET.fromstring(raw)
    result = {}
    for child in root:
        result[child.tag] = child.text
    return result


def _from_csv(raw: str) -> dict:
    reader = csv.DictReader(io.StringIO(raw))
    rows = list(reader)
    return rows[0] if rows else {}


def _normalize_to_json(raw: str, fmt: str) -> str:
    fmt = fmt.upper()
    if fmt == 'JSON':
        parsed = json.loads(raw)
    elif fmt == 'XML':
        parsed = _from_xml(raw)
    elif fmt == 'CSV':
        parsed = _from_csv(raw)
    else:
        raise ValueError(f"Formato desconhecido: {fmt}")
    return json.dumps(parsed, ensure_ascii=False)


# ── Implementação do serviço gRPC ─────────────────────────────────────────────

class PreprocessingServicer(preprocessing_pb2_grpc.PreprocessingServiceServicer):

    def PreprocessData(self, request, context):
        """Normaliza, valida e classifica uma leitura bruta de sensor."""
        try:
            sensor_id = request.sensor_id
            data_type = request.data_type.upper()
            value     = request.value
            unit      = _get_unit(data_type, request.unit)

            hist = _get_history(sensor_id, data_type)
            z    = _compute_zscore(hist, value)
            hist.append(value)

            quality    = _determine_quality(z, value, data_type)
            is_outlier = abs(z) > 3.0
            ts         = datetime.now(timezone.utc).isoformat()

            log.info(f"PreprocessData: {sensor_id}/{data_type}={value}{unit} "
                     f"quality={quality} z={z:.2f} outlier={is_outlier}")

            return preprocessing_pb2.ProcessedReading(
                sensor_id        = sensor_id,
                data_type        = data_type,
                normalized_value = value,
                normalized_unit  = unit,
                quality          = quality,
                is_outlier       = is_outlier,
                z_score          = z,
                timestamp        = ts,
                status           = 'OK',
                error_message    = ''
            )

        except Exception as e:
            log.error(f"Erro em PreprocessData: {e}")
            return preprocessing_pb2.ProcessedReading(
                sensor_id     = request.sensor_id,
                data_type     = request.data_type,
                status        = 'ERROR',
                error_message = str(e)
            )

    def ConvertFormat(self, request, context):
        """Converte raw_data de source_format para JSON normalizado."""
        try:
            normalized = _normalize_to_json(request.raw_data, request.source_format)
            log.info(f"ConvertFormat: {request.source_format} → JSON OK")
            return preprocessing_pb2.FormatResponse(
                normalized_json = normalized,
                success         = True,
                error_message   = ''
            )
        except Exception as e:
            log.error(f"Erro em ConvertFormat: {e}")
            return preprocessing_pb2.FormatResponse(
                normalized_json = '',
                success         = False,
                error_message   = str(e)
            )


# ── Arranque do servidor ──────────────────────────────────────────────────────

def serve(port: int = 50051):
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    preprocessing_pb2_grpc.add_PreprocessingServiceServicer_to_server(
        PreprocessingServicer(), server
    )
    server.add_insecure_port(f'[::]:{port}')
    server.start()
    log.info(f"PreprocessingService gRPC em escuta na porta {port}")
    try:
        server.wait_for_termination()
    except KeyboardInterrupt:
        log.info("A encerrar PreprocessingService...")
        server.stop(0)


if __name__ == '__main__':
    serve()
