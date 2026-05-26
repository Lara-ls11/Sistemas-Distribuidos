@echo off
set ROOT=%~dp0

echo A abrir todos os componentes do TP2...

start "PreprocessingService" cmd /k "cd /d %ROOT%PreprocessingService && python preprocessing_server.py"
timeout /t 2 /nobreak >nul

start "AnalysisService" cmd /k "cd /d %ROOT%AnalysisService && python analysis_server.py"
timeout /t 2 /nobreak >nul

start "Servidor" cmd /k "cd /d %ROOT%Servidor && dotnet run"
timeout /t 4 /nobreak >nul

start "Gateway zona1" cmd /k "cd /d %ROOT%Gateway && dotnet run zona1"
timeout /t 3 /nobreak >nul

start "Sensor zona1" cmd /k "cd /d %ROOT%Sensor && dotnet run zona1 auto"
