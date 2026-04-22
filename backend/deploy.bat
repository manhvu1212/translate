@echo off
setlocal

set BACKEND_DIR=%~dp0

echo [1/4] Creating virtual environment...
python -m venv "%BACKEND_DIR%.venv"
if errorlevel 1 (echo ERROR: python not found & exit /b 1)

echo [2/4] Installing dependencies...
call "%BACKEND_DIR%.venv\Scripts\activate.bat"
pip install --no-cache-dir -e "%BACKEND_DIR:~0,-1%"
if errorlevel 1 (echo ERROR: pip install failed & exit /b 1)

echo [3/4] Checking ffmpeg...
where ffmpeg >nul 2>&1
if errorlevel 1 (
    echo WARNING: ffmpeg not found. STT may fail.
    echo Install from: https://ffmpeg.org/download.html
) else (
    echo ffmpeg OK
)

echo [4/4] Checking GPU...
python -c "import ctranslate2; count=ctranslate2.get_cuda_device_count(); exit(0 if count > 0 else 1)" 2>nul
if errorlevel 1 (
    echo WARNING: GPU not available, will use CPU.
    echo Set WHISPER_DEVICE=cpu in .env if not already set.
) else (
    echo GPU OK - set WHISPER_DEVICE=cuda in .env to enable
)

echo [5/5] Checking Ollama + gemma4:e4b...
curl -s -o nul -w "%%{http_code}" http://localhost:11434 2>nul | findstr /i "200" >nul
if errorlevel 1 (
    echo WARNING: Ollama not running at http://localhost:11434. Start Ollama first.
) else (
    curl -s -X POST http://localhost:11434/api/generate -H "Content-Type: application/json" -d "{\"model\":\"gemma4:e4b\",\"prompt\":\"hi\",\"stream\":false}" 2>nul | python -c "import sys,json; d=json.load(sys.stdin); exit(0 if d.get('response') else 1)" 2>nul
    if errorlevel 1 (
        echo WARNING: gemma4:e4b did not respond. Run: ollama pull gemma4:e4b
    ) else (
        echo Ollama + gemma4:e4b OK
    )
)

echo.
echo Deploy done. Run: run.bat
endlocal
