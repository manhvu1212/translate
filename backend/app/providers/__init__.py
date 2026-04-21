from app.providers.base import LLMProvider
from app.providers.gemma import GemmaProvider
from app.config import settings

_provider: LLMProvider | None = None


def get_provider() -> LLMProvider:
    global _provider
    if _provider is None:
        _provider = GemmaProvider(
            host=settings.ollama_host,
            text_model=settings.gemma_model,
            vision_model=settings.gemma_vision_model,
        )
    return _provider
