from fastapi import APIRouter, File, Form, HTTPException, UploadFile

from app.providers import get_provider
from app.schemas import OcrResponse

router = APIRouter(prefix="/ocr", tags=["ocr"])

MAX_IMAGE_BYTES = 10 * 1024 * 1024  # 10 MB


@router.post("", response_model=OcrResponse)
async def ocr(
    image: UploadFile = File(...),
    target: str = Form("en"),
) -> OcrResponse:
    if image.content_type and not image.content_type.startswith("image/"):
        raise HTTPException(400, "File must be an image")
    data = await image.read()
    if len(data) > MAX_IMAGE_BYTES:
        raise HTTPException(413, "Image exceeds 10 MB limit")
    result = await get_provider().ocr_translate(data, target)
    return OcrResponse(
        extracted=result.get("extracted", ""),
        translated=result.get("translated", ""),
        target=target,
    )
