@echo off
REM Instala dependências Python e gera ficheiros gRPC
echo A instalar grpcio e grpcio-tools...
pip install grpcio grpcio-tools

echo.
echo A gerar ficheiros do PreprocessingService...
cd PreprocessingService
python -m grpc_tools.protoc -I. --python_out=. --grpc_python_out=. preprocessing.proto
echo   Gerados: preprocessing_pb2.py, preprocessing_pb2_grpc.py
cd ..

echo.
echo A gerar ficheiros do AnalysisService...
cd AnalysisService
python -m grpc_tools.protoc -I. --python_out=. --grpc_python_out=. analysis.proto
echo   Gerados: analysis_pb2.py, analysis_pb2_grpc.py
cd ..

echo.
echo Configuração Python concluída!
pause
