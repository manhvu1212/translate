# Deploy — Backend (Windows PC + NVIDIA GPU)

## Requirements

- Windows 10/11 with [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed
- Ollama running natively on the PC (manual setup, GPU-accelerated)
- Gemma 4 model pulled: `ollama pull gemma4:e4b`

## First-time setup

```powershell
# 1. Go to backend folder
cd backend

# 2. Create production env file
copy .env.production.example .env.production
# Edit .env.production as needed (CORS_ORIGINS, WHISPER_MODEL, etc.)

# 3. Build the image
docker compose build

# 4. Start
docker compose up -d
```

## Verify it's running

```powershell
# Health check
curl http://localhost:8000/health

# Expected:
# {"status":"ok","text_model":"gemma4:e4b","vision_model":"gemma4:e4b","num_ctx":131072,...}
```

## Day-to-day commands

```powershell
# Start
docker compose up -d

# Stop
docker compose down

# View logs
docker compose logs -f

# Restart after code changes
docker compose build && docker compose up -d

# Check status
docker compose ps
```

## Auto-start on Windows boot

Docker Desktop has a "Start Docker Desktop when you log in" option in Settings → General.
Once Docker Desktop auto-starts, containers with `restart: unless-stopped` will start automatically.

## Architecture

```
Internet / LAN
      │
      ▼
:8000 FastAPI container (Docker)
      │  OLLAMA_HOST=http://host.docker.internal:11434
      ▼
Ollama (native Windows, NVIDIA GPU)
      │
      ▼
gemma4:e4b weights
```

`host.docker.internal` is a special DNS name Docker Desktop provides on Windows and macOS
that resolves to the host machine's IP from inside any container.

## Expose to internet (optional)

For access outside the LAN, use [Cloudflare Tunnel](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/):

```powershell
# Install cloudflared
winget install Cloudflare.cloudflared

# Authenticate (one time)
cloudflared tunnel login

# Create tunnel pointing to backend
cloudflared tunnel create translate-backend
cloudflared tunnel route dns translate-backend api.yourdomain.com

# Run tunnel (add to startup or Docker Compose later)
cloudflared tunnel run translate-backend
```

Then update `CORS_ORIGINS` in `.env.production` with your domain.
