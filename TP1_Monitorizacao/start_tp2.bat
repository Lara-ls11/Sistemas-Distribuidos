@echo off
REM ════════════════════════════════════════════════════════════════
REM  TP2 – Arranque completo do sistema
REM  Ordem: RabbitMQ → Serviços RPC (Python) → Servidor → Gateway → Sensor
REM ════════════════════════════════════════════════════════════════

echo.
echo ╔══════════════════════════════════════════╗
echo ║   TP2 - Sistema One Health               ║
echo ╚══════════════════════════════════════════╝
echo.

REM ── 1. RabbitMQ via Docker ────────────────────────────────────────
echo [1/5] A iniciar RabbitMQ...
docker compose up -d rabbitmq
timeout /t 10 /nobreak >nul
echo     RabbitMQ em http://localhost:15672

REM ── 2. Serviço de Pré-processamento (Python gRPC, porta 50051) ───
echo [2/5] A iniciar PreprocessingService (porta 50051)...
cd PreprocessingService
if not exist preprocessing_pb2.py (
    echo     A gerar ficheiros gRPC...
    python -m grpc_tools.protoc -I. --python_out=. --grpc_python_out=. preprocessing.proto
)
start "PreprocessingService" cmd /k "python preprocessing_server.py"
cd ..
timeout /t 3 /nobreak >nul

REM ── 3. Serviço de Análise (Python gRPC, porta 50052) ─────────────
echo [3/5] A iniciar AnalysisService (porta 50052)...
cd AnalysisService
if not exist analysis_pb2.py (
    echo     A gerar ficheiros gRPC...
    python -m grpc_tools.protoc -I. --python_out=. --grpc_python_out=. analysis.proto
)
start "AnalysisService" cmd /k "python analysis_server.py"
cd ..
timeout /t 3 /nobreak >nul

REM ── 4. Servidor Principal (C#, porta 5002) ────────────────────────
echo [4/5] A iniciar Servidor...
start "Servidor" cmd /k "cd Servidor && dotnet run"
timeout /t 5 /nobreak >nul

REM ── 5. Gateway (C#, zona1) ───────────────────────────────────────
echo [5/5] A iniciar Gateway (zona1)...
start "Gateway zona1" cmd /k "cd Gateway && dotnet run zona1"
timeout /t 3 /nobreak >nul

echo.
echo ════════════════════════════════════════════════════
echo  Sistema iniciado! Para iniciar sensores, corra:
echo    cd Sensor ^&^& dotnet run zona1 auto
echo ════════════════════════════════════════════════════
echo.
pause
