# Translate — Gemma-powered multilingual app

Text / OCR / Voice translation across **web, Android, iOS**, sharing one TypeScript API client and one Python backend.

Runs on **Gemma 4** (released 2026-04-02) with a provider abstraction so the model can be swapped via a single env var — Gemma 3 remains supported for legacy setups.

## Stack

| Layer | Tech |
|------|------|
| LLM | **Gemma 4** `e4b` (9.6 GB, 128K ctx, text + image + audio) via [Ollama](https://ollama.com) |
| STT | [faster-whisper](https://github.com/SYSTRAN/faster-whisper) — can later be replaced by native Gemma 4 audio |
| Backend | FastAPI (Python 3.11+) |
| Web | Next.js 15 + Tailwind |
| Mobile | Expo 52 / React Native (Android + iOS) |
| Shared | TypeScript monorepo via pnpm workspaces |

```
backend/           FastAPI + Gemma provider + Whisper STT
packages/shared/   TS types + fetch-based API client (used by web & mobile)
web/               Next.js app
mobile/            Expo app — tabs: Text / Image / Voice
```

## Prerequisites

- **Node** 20+ and **pnpm** 9+
- **Python** 3.11+
- **Ollama** running locally
- For mobile: **Expo Go** app (quick start) or Android Studio / Xcode (native builds)

## Quick start

### 1. LLM

```bash
ollama pull gemma4:e4b       # 9.6 GB — recommended default (vision + audio)
# Alternatives:
# ollama pull gemma4:e2b     # 7.2 GB — on-device / laptops
# ollama pull gemma4:26b     # 18 GB  — MoE, 4B active params
# ollama pull gemma4:31b     # 20 GB  — flagship dense
# Legacy: ollama pull gemma3:4b
ollama serve                 # usually runs automatically
```

### 2. Backend

```bash
cd backend
python -m venv .venv
# Windows: .venv\Scripts\activate    macOS/Linux: source .venv/bin/activate
pip install -e .
cp .env.example .env
uvicorn app.main:app --reload --port 8000
```

Verify: http://localhost:8000/health

### 3. Install JS workspaces

From the repo root:

```bash
pnpm install
```

### 4. Web

```bash
cp web/.env.local.example web/.env.local
pnpm dev:web
```

Open http://localhost:3000

### 5. Mobile

```bash
pnpm dev:mobile
```

Scan the QR with **Expo Go** (Android or iOS).

> **Important for physical devices:** `localhost` inside the phone isn't your laptop. Edit `mobile/app.json` → `expo.extra.apiUrl` and replace with your laptop's LAN IP, e.g. `http://192.168.1.50:8000`. Make sure the backend is reachable from the phone.

## Swapping models

The provider abstraction in [backend/app/providers/base.py](backend/app/providers/base.py) absorbs model swaps — zero code changes required.

To switch between Gemma 4 variants or back to Gemma 3, edit `backend/.env`:

```env
GEMMA_MODEL=gemma4:e4b        # or gemma4:e2b / gemma4:26b / gemma4:31b / gemma3:4b
GEMMA_VISION_MODEL=gemma4:e4b # must be a vision-capable tag
```

Then restart the backend.

## API surface

| Method | Path | Purpose |
|-------|------|---------|
| POST | `/translate` | Plain JSON translation |
| POST | `/translate/stream` | Token-streamed translation |
| POST | `/ocr` | Multipart image → extracted + translated text (Gemma 3 vision) |
| POST | `/stt` | Multipart audio → transcript + optional translation |
| GET  | `/languages` | Supported language list |
| GET  | `/health` | Status + loaded model names |

See [backend/README.md](backend/README.md) for more.

## Notes

- **OCR** is done end-to-end by Gemma 3's vision capability — no Tesseract dependency.
- **Voice** is a two-stage pipeline: Whisper for transcription → Gemma for translation. Whisper can optionally be swapped for a Gemma-native audio path later.
- The shared client ([packages/shared/src/client.ts](packages/shared/src/client.ts)) is the single source of truth for API contracts — both web and mobile import from it.
