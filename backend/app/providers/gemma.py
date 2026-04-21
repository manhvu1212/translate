import base64
import json
from typing import AsyncIterator

import httpx

from app.languages import LANGUAGES
from app.providers.base import LLMProvider

_LANG_NAME = {l["code"]: l["name"] for l in LANGUAGES}


def _lang_name(code: str) -> str:
    if code == "auto":
        return "auto-detected language"
    return _LANG_NAME.get(code, code)


def _translate_prompt(text: str, source: str, target: str) -> str:
    return (
        f"Translate the following text from {_lang_name(source)} to {_lang_name(target)}. "
        "Return ONLY the translation — no explanations, no quotes, no source language labels.\n\n"
        f"Text:\n{text}"
    )


def _ocr_prompt(target: str) -> str:
    target_name = _lang_name(target)
    return (
        "You are an OCR and translation assistant.\n"
        "Task 1: transcribe EVERY line of visible text in this image, in the order it appears, "
        "exactly as written. Include text in any language. Separate lines with \\n.\n"
        f"Task 2: translate the full transcribed text into {target_name}. "
        "Preserve line breaks. If a line is already in the target language, keep it unchanged.\n"
        "Output format: ONLY a single minified JSON object, no markdown, no code fences, no commentary:\n"
        '{"extracted":"<every line, joined by \\n>","translated":"<every line translated, joined by \\n>"}'
    )


class GemmaProvider(LLMProvider):
    def __init__(self, host: str, text_model: str, vision_model: str, num_ctx: int):
        self.host = host.rstrip("/")
        self.text_model = text_model
        self.vision_model = vision_model
        self.num_ctx = num_ctx

    def _opts(self, temperature: float) -> dict:
        return {"temperature": temperature, "num_ctx": self.num_ctx}

    async def translate(self, text: str, source: str, target: str) -> str:
        payload = {
            "model": self.text_model,
            "prompt": _translate_prompt(text, source, target),
            "stream": False,
            "options": self._opts(0.2),
        }
        async with httpx.AsyncClient(timeout=120) as client:
            r = await client.post(f"{self.host}/api/generate", json=payload)
            r.raise_for_status()
            return r.json().get("response", "").strip()

    async def translate_stream(
        self, text: str, source: str, target: str
    ) -> AsyncIterator[str]:
        payload = {
            "model": self.text_model,
            "prompt": _translate_prompt(text, source, target),
            "stream": True,
            "options": self._opts(0.2),
        }
        async with httpx.AsyncClient(timeout=None) as client:
            async with client.stream("POST", f"{self.host}/api/generate", json=payload) as r:
                r.raise_for_status()
                async for line in r.aiter_lines():
                    if not line:
                        continue
                    try:
                        chunk = json.loads(line)
                    except json.JSONDecodeError:
                        continue
                    piece = chunk.get("response", "")
                    if piece:
                        yield piece
                    if chunk.get("done"):
                        break

    async def ocr_translate(self, image_bytes: bytes, target: str) -> dict:
        b64 = base64.b64encode(image_bytes).decode("ascii")
        payload = {
            "model": self.vision_model,
            "prompt": _ocr_prompt(target),
            "images": [b64],
            "stream": False,
            "options": self._opts(0.1),
        }
        async with httpx.AsyncClient(timeout=180) as client:
            r = await client.post(f"{self.host}/api/generate", json=payload)
            r.raise_for_status()
            raw = r.json().get("response", "").strip()
        try:
            return json.loads(raw)
        except json.JSONDecodeError:
            # Model sometimes wraps JSON in prose — extract the first {...} span.
            start = raw.find("{")
            end = raw.rfind("}")
            if start >= 0 and end > start:
                try:
                    return json.loads(raw[start : end + 1])
                except json.JSONDecodeError:
                    pass
            return {"extracted": raw, "translated": ""}
