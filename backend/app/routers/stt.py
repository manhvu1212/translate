import os
import tempfile

from fastapi import APIRouter, File, Form, HTTPException, UploadFile

from app.providers import get_provider
from app.schemas import SttResponse
from app.services import stt

router = APIRouter(prefix="/stt", tags=["stt"])

MAX_AUDIO_BYTES = 25 * 1024 * 1024  # 25 MB


@router.post("", response_model=SttResponse)
async def speech_to_text(
    audio: UploadFile = File(...),
    language: str = Form("auto"),
    target: str | None = Form(None),
) -> SttResponse:
    data = await audio.read()
    if len(data) > MAX_AUDIO_BYTES:
        raise HTTPException(413, "Audio exceeds 25 MB limit")

    suffix = os.path.splitext(audio.filename or "")[1] or ".wav"
    tmp = tempfile.NamedTemporaryFile(delete=False, suffix=suffix)
    try:
        tmp.write(data)
        tmp.flush()
        tmp.close()
        result = stt.transcribe(tmp.name, language)
    finally:
        try:
            os.unlink(tmp.name)
        except OSError:
            pass

    translated = None
    if target and result["text"]:
        translated = await get_provider().translate(result["text"], result["language"], target)

    return SttResponse(
        text=result["text"],
        language=result["language"],
        translated=translated,
        target=target,
    )
