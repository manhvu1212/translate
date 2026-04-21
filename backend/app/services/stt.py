from functools import lru_cache

from faster_whisper import WhisperModel

from app.config import settings


@lru_cache(maxsize=1)
def _model() -> WhisperModel:
    return WhisperModel(
        settings.whisper_model,
        device=settings.whisper_device,
        compute_type=settings.whisper_compute_type,
    )


def transcribe(audio_path: str, language: str | None = None) -> dict:
    lang = None if not language or language == "auto" else language
    segments, info = _model().transcribe(audio_path, language=lang, vad_filter=True)
    text = "".join(seg.text for seg in segments).strip()
    return {"text": text, "language": info.language}
