from fastapi import APIRouter

from app.config import settings
from app.languages import LANGUAGES

router = APIRouter(tags=["meta"])


@router.get("/languages")
def languages():
    return {"languages": LANGUAGES}


@router.get("/health")
def health():
    return {
        "status": "ok",
        "text_model": settings.gemma_model,
        "vision_model": settings.gemma_vision_model,
        "num_ctx": settings.gemma_num_ctx,
        "whisper_model": settings.whisper_model,
    }
