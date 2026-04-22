@echo off
setlocal

set BACKEND_DIR=%~dp0

echo [1/4] Creating virtual environment...
python -m venv "%BACKEND_DIR%.venv"
if errorlevel 1 (echo ERROR: python not found & exit /b 1)

echo [2/4] Installing dependencies...
call "%BACKEND_DIR%.venv\Scripts\activate.bat"
pip install --no-cache-dir -e "%BACKEND_DIR%"
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
python -c "from faster_whisper import WhisperModel; WhisperModel('base', device='cuda', compute_type='float16'); print('GPU OK')" 2>nul
if errorlevel 1 (
    echo WARNING: GPU not available, will use CPU.
    echo Set WHISPER_DEVICE=cpu in .env if not already set.
) else (
    echo GPU OK - set WHISPER_DEVICE=cuda in .env to enable
)

echo.
echo Deploy done. Run: run.bat
endlocal
