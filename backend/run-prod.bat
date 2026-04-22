@echo off
setlocal

set BACKEND_DIR=%~dp0
set TUNNEL_NAME=translate-backend
set CREDENTIALS=%USERPROFILE%\.cloudflared\%TUNNEL_NAME%.json

if not exist "%BACKEND_DIR%.venv" (
    echo ERROR: .venv not found. Run deploy.bat first.
    exit /b 1
)

if not exist "%BACKEND_DIR%.env.production" (
    echo ERROR: .env.production not found.
    exit /b 1
)

where cloudflared >nul 2>&1
if errorlevel 1 (
    echo ERROR: cloudflared not found.
    echo Install: winget install Cloudflare.cloudflared
    exit /b 1
)

if not exist "%CREDENTIALS%" (
    echo ERROR: Tunnel credentials not found at %CREDENTIALS%
    echo Run setup once:
    echo   cloudflared tunnel login
    echo   cloudflared tunnel create %TUNNEL_NAME%
    echo   cloudflared tunnel route dns %TUNNEL_NAME% api.nguyenmanhvu.name.vn
    exit /b 1
)

call "%BACKEND_DIR%.venv\Scripts\activate.bat"

echo Starting backend on http://0.0.0.0:8000 ...
start "translate-backend" uvicorn app.main:app --host 0.0.0.0 --port 8000 --log-level info

echo Waiting for backend...
:wait
ping -n 2 127.0.0.1 >nul
python -c "import urllib.request; urllib.request.urlopen('http://localhost:8000/health')" >nul 2>&1
if errorlevel 1 goto wait

echo Starting Cloudflare Named Tunnel [api.nguyenmanhvu.name.vn] ...
cloudflared tunnel --config "%BACKEND_DIR%cloudflared-config.yml" run

endlocal
