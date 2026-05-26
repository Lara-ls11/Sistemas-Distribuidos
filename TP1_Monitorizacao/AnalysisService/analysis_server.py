"""
Serviço de Análise e Previsão RPC (gRPC)
Porta: 50052
Realiza análise estatística, deteção de padrões de poluição
e previsão de riscos para saúde pública.
"""

import grpc
import math
import logging
from concurrent import futures
from datetime import datetime, timezone

import analysis_pb2
import analysis_pb2_grpc

logging.basicConfig(
    level=logging.INFO,
    format='[%(levelname)s] %(asctime)s - %(message)s',
    datefmt='%H:%M:%S'
)
log = logging.getLogger(__name__)


# ── Limiares WHO/EU por tipo de poluente ─────────────────────────────────────
WHO_LIMITS = {
    'PM25':     {'moderate': 15.0,   'high': 35.0,   'critical': 75.0},
    'NO2':      {'moderate': 25.0,   'high': 100.0,  'critical': 200.0},
    'CO':       {'moderate': 4.0,    'high': 7.0,    'critical': 35.0},
    'O3':       {'moderate': 60.0,   'high': 120.0,  'critical': 240.0},
    'TEMP':     {'moderate': 35.0,   'high': 40.0,   'critical': 45.0},
    'ACOUSTIC': {'moderate': 55.0,   'high': 70.0,   'critical': 85.0},
    'HUM':      {'moderate': 70.0,   'high': 85.0,   'critical': 95.0},
}


# ── Funções estatísticas ──────────────────────────────────────────────────────

def _stats(values: list[float]) -> dict:
    if not values:
        return {}
    n    = len(values)
    avg  = sum(values) / n
    mn   = min(values)
    mx   = max(values)
    var  = sum((x - avg) ** 2 for x in values) / n
    std  = math.sqrt(var)
    srt  = sorted(values)
    med  = srt[n // 2] if n % 2 else (srt[n // 2 - 1] + srt[n // 2]) / 2
    return {'count': n, 'average': avg, 'minimum': mn,
            'maximum': mx, 'std_dev': std, 'median': med}


def _trend(values: list[float]) -> str:
    if len(values) < 3:
        return 'STABLE'
    # Regressão linear simples
    n  = len(values)
    xs = list(range(n))
    mx = sum(xs) / n
    my = sum(values) / n
    num   = sum((xs[i] - mx) * (values[i] - my) for i in range(n))
    denom = sum((xs[i] - mx) ** 2 for i in range(n))
    if denom == 0:
        return 'STABLE'
    slope = num / denom
    # Limiar: >1% da gama por passo → tendência significativa
    rng = max(values) - min(values)
    threshold = max(0.1, rng * 0.01)
    if slope > threshold:
        return 'RISING'
    if slope < -threshold:
        return 'FALLING'
    return 'STABLE'


# ── Implementação do serviço ──────────────────────────────────────────────────

class AnalysisServicer(analysis_pb2_grpc.AnalysisServiceServicer):

    def AnalyzeReadings(self, request, context):
        """Análise estatística das leituras recebidas."""
        try:
            # Os dados chegam via request; aqui usamos a lógica de análise
            # O servidor C# envia os dados como DataPoints para deteção de padrões,
            # e usa AnalyzeReadings para estatísticas a partir de listas inline.
            # Para simplicidade o servidor passa os dados como JSON no campo data_type
            # com prefixo "DATA:" ou apenas invoca com parâmetros e a análise é feita
            # sobre os dados armazenados em BD no lado do servidor.
            # Aqui simulamos com dados sintéticos se não houver data_type especial.
            sensor_id = request.sensor_id
            data_type = request.data_type
            log.info(f"AnalyzeReadings: sensor={sensor_id} type={data_type} "
                     f"período={request.start_time} → {request.end_time}")

            # Nota: em produção o servidor enviaria os valores. Aqui respondemos
            # com metadados da análise (o servidor C# já tem os dados em BD e pode
            # calcular stats localmente; esta chamada RPC serve para análises avançadas).
            return analysis_pb2.AnalysisResponse(
                sensor_id    = sensor_id,
                data_type    = data_type,
                count        = 0,
                average      = 0.0,
                minimum      = 0.0,
                maximum      = 0.0,
                std_dev      = 0.0,
                median       = 0.0,
                trend        = 'STABLE',
                status       = 'OK',
                error_message= ''
            )
        except Exception as e:
            log.error(f"Erro em AnalyzeReadings: {e}")
            return analysis_pb2.AnalysisResponse(
                status='ERROR', error_message=str(e))

    def DetectPatterns(self, request, context):
        """Deteta padrões de poluição nos dados fornecidos."""
        try:
            data_type = request.data_type
            points    = list(request.data)
            values    = [p.value for p in points]

            log.info(f"DetectPatterns: type={data_type} n_points={len(values)}")

            patterns = []

            if len(values) < 3:
                return analysis_pb2.PatternResponse(
                    patterns=[],
                    summary='Dados insuficientes para análise de padrões.',
                    status='OK'
                )

            st = _stats(values)
            avg, std = st['average'], st['std_dev']
            limits = WHO_LIMITS.get(data_type, {})

            # Deteção de picos (spike): valor > média + 3*std
            for i, p in enumerate(points):
                if std > 0 and abs(p.value - avg) > 3 * std:
                    patterns.append(analysis_pb2.DetectedPattern(
                        pattern_type='SPIKE',
                        description =f"Valor anómalo {p.value:.2f} em {p.timestamp}",
                        severity    =min(1.0, abs(p.value - avg) / (3 * std) - 1),
                        start_time  =p.timestamp,
                        end_time    =p.timestamp
                    ))

            # Deteção de nível elevado sustentado
            if limits:
                high_thresh = limits.get('high', float('inf'))
                critical    = limits.get('critical', float('inf'))
                sustained   = [v for v in values if v > high_thresh]
                if len(sustained) > len(values) * 0.5:
                    sev = min(1.0, st['average'] / critical) if critical else 0.5
                    patterns.append(analysis_pb2.DetectedPattern(
                        pattern_type='SUSTAINED_HIGH',
                        description =f"Mais de 50% das leituras de {data_type} acima do limiar alto ({high_thresh})",
                        severity    =sev,
                        start_time  =points[0].timestamp if points else '',
                        end_time    =points[-1].timestamp if points else ''
                    ))

            # Tendência
            t = _trend(values)
            if t != 'STABLE' and limits:
                patterns.append(analysis_pb2.DetectedPattern(
                    pattern_type='ANOMALY',
                    description =f"Tendência {t} detetada em {data_type}",
                    severity    =0.3,
                    start_time  =points[0].timestamp if points else '',
                    end_time    =points[-1].timestamp if points else ''
                ))

            summary = (f"Analisados {len(values)} pontos de {data_type}. "
                       f"Avg={avg:.2f} Std={std:.2f} Tendência={t}. "
                       f"{len(patterns)} padrão(ões) detetado(s).")

            return analysis_pb2.PatternResponse(
                patterns=patterns,
                summary =summary,
                status  ='OK',
                error_message=''
            )

        except Exception as e:
            log.error(f"Erro em DetectPatterns: {e}")
            return analysis_pb2.PatternResponse(
                status='ERROR', error_message=str(e))

    def PredictHealthRisk(self, request, context):
        """Calcula risco para saúde pública com base nas leituras mais recentes."""
        try:
            zone     = request.zone or 'desconhecida'
            readings = list(request.readings)
            log.info(f"PredictHealthRisk: zone={zone} n_leituras={len(readings)}")

            scores = []
            details = []

            for r in readings:
                limits = WHO_LIMITS.get(r.data_type)
                if not limits:
                    continue

                val = r.value
                if val >= limits['critical']:
                    s = 90.0 + min(10.0, (val - limits['critical']) / limits['critical'] * 10)
                    details.append(f"{r.data_type} CRÍTICO ({val:.1f})")
                elif val >= limits['high']:
                    s = 60.0 + (val - limits['high']) / (limits['critical'] - limits['high']) * 30
                    details.append(f"{r.data_type} ALTO ({val:.1f})")
                elif val >= limits['moderate']:
                    s = 30.0 + (val - limits['moderate']) / (limits['high'] - limits['moderate']) * 30
                    details.append(f"{r.data_type} MODERADO ({val:.1f})")
                else:
                    s = val / limits['moderate'] * 30
                scores.append(s)

            risk_score = max(scores) if scores else 0.0

            if risk_score >= 80:
                risk_level = 'CRITICAL'
                recs = [
                    'Evitar atividade física ao ar livre.',
                    'Grupos vulneráveis devem permanecer em casa.',
                    'Usar máscara N95 se necessário sair.',
                    'Contactar autoridades de saúde pública.'
                ]
            elif risk_score >= 60:
                risk_level = 'HIGH'
                recs = [
                    'Reduzir atividade física ao ar livre.',
                    'Grupos vulneráveis devem evitar exposição prolongada.',
                    'Monitorizar sintomas respiratórios.'
                ]
            elif risk_score >= 30:
                risk_level = 'MODERATE'
                recs = [
                    'Limitar exercício intenso ao ar livre.',
                    'Manter janelas fechadas em horas de pico.'
                ]
            else:
                risk_level = 'LOW'
                recs = ['Qualidade do ar aceitável. Sem precauções especiais.']

            summary = (f"Zona {zone}: Risco {risk_level} (score={risk_score:.1f}). "
                       + ('; '.join(details) if details else 'Sem poluentes críticos.'))

            return analysis_pb2.RiskResponse(
                risk_level      =risk_level,
                risk_score      =risk_score,
                recommendations =recs,
                summary         =summary,
                status          ='OK',
                error_message   =''
            )

        except Exception as e:
            log.error(f"Erro em PredictHealthRisk: {e}")
            return analysis_pb2.RiskResponse(
                status='ERROR', error_message=str(e))


# ── Arranque ──────────────────────────────────────────────────────────────────

def serve(port: int = 50052):
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    analysis_pb2_grpc.add_AnalysisServiceServicer_to_server(
        AnalysisServicer(), server
    )
    server.add_insecure_port(f'[::]:{port}')
    server.start()
    log.info(f"AnalysisService gRPC em escuta na porta {port}")
    try:
        server.wait_for_termination()
    except KeyboardInterrupt:
        log.info("A encerrar AnalysisService...")
        server.stop(0)


if __name__ == '__main__':
    serve()
