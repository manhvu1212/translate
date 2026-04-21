# Backend

FastAPI + **Gemma 4** (via Ollama) + faster-whisper.

## Setup

1. Install [Ollama](https://ollama.com) and pull a model:
   ```bash
   ollama pull gemma4:e4b       # recommended (9.6 GB, vision + audio)
   # or: ollama pull gemma4:e2b (7.2 GB) / gemma4:26b / gemma4:31b / gemma3:4b
   ```
2. Create venv and install deps:
   ```bash
   python -m venv .venv
   .venv\Scripts\activate      # Windows
   # source .venv/bin/activate  # macOS/Linux
   pip install -e .
   ```
3. Copy env: `cp .env.example .env`
4. Run:
   ```bash
   uvicorn app.main:app --reload --port 8000
   ```

## Switching models

Edit `.env`:

```env
GEMMA_MODEL=gemma4:e4b         # or gemma4:e2b / gemma4:26b / gemma4:31b / gemma3:4b
GEMMA_VISION_MODEL=gemma4:e4b  # must be vision-capable (all gemma4 tags qualify)
```

Restart uvicorn. No code changes — the provider in `app/providers/` absorbs the swap.

## Endpoints

- `POST /translate` — `{text, source, target}` → `{translated}`
- `POST /translate/stream` — same body, streamed plain-text chunks
- `POST /ocr` — multipart `image` + `target` → `{extracted, translated}`
- `POST /stt` — multipart `audio` + `language` + optional `target` → `{text, language, translated?}`
- `GET /languages` — supported language codes
- `GET /health` — status + loaded models
