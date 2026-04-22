@echo off
setlocal

set BACKEND_DIR=%~dp0

if not exist "%BACKEND_DIR%.venv" (
    echo ERROR: .venv not found. Run deploy.bat first.
    exit /b 1
)

if not exist "%BACKEND_DIR%.env" (
    echo ERROR: .env not found. Create it from .env.example.
    exit /b 1
)

call "%BACKEND_DIR%.venv\Scripts\activate.bat"

echo Starting backend on http://0.0.0.0:8000 ...
start "translate-backend" uvicorn app.main:app --host 0.0.0.0 --port 8000 --log-level info

:: Wait for backend to be ready
echo Waiting for backend...
:wait
ping -n 2 127.0.0.1 >nul
python -c "import urllib.request; urllib.request.urlopen('http://localhost:8000/health')" >nul 2>&1
if errorlevel 1 goto wait

:: Start Cloudflare Tunnel
where cloudflared >nul 2>&1
if errorlevel 1 (
    echo WARNING: cloudflared not found. Skipping tunnel.
    echo Install from: https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/
) else (
    echo Starting Cloudflare Tunnel...
    cloudflared tunnel --url http://localhost:8000
)

endlocal
