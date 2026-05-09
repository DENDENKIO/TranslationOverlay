@echo off
chcp 932 > nul
cd /d "%~dp0"

echo [1/2] Starting Argos Translation Server...
start "ArgosServer" python argos_server.py

echo [2/2] Waiting for server startup (5 seconds)...
timeout /t 5 /nobreak > nul

echo Server started.
echo Access: http://localhost:5000
echo.
echo Press any key to open translation overlay...
pause > nul

echo Launching overlay...
start "" overlay.exe