@echo off
REM Gera os ficheiros Python a partir do ficheiro .proto
pip install grpcio-tools
python -m grpc_tools.protoc -I. --python_out=. --grpc_python_out=. analysis.proto
echo Ficheiros gerados: analysis_pb2.py e analysis_pb2_grpc.py
pause
