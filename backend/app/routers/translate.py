from fastapi import APIRouter
from fastapi.responses import StreamingResponse

from app.providers import get_provider
from app.schemas import TranslateRequest, TranslateResponse

router = APIRouter(prefix="/translate", tags=["translate"])


@router.post("", response_model=TranslateResponse)
async def translate(req: TranslateRequest) -> TranslateResponse:
    out = await get_provider().translate(req.text, req.source, req.target)
    return TranslateResponse(translated=out, source=req.source, target=req.target)


@router.post("/stream")
async def translate_stream(req: TranslateRequest):
    async def gen():
        async for piece in get_provider().translate_stream(req.text, req.source, req.target):
            yield piece

    return StreamingResponse(gen(), media_type="text/plain; charset=utf-8")
