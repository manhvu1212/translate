from abc import ABC, abstractmethod
from typing import AsyncIterator


class LLMProvider(ABC):
    """Abstract LLM provider. Swap implementations to move between Gemma 3, Gemma 4, or others."""

    @abstractmethod
    async def translate(self, text: str, source: str, target: str) -> str: ...

    @abstractmethod
    def translate_stream(
        self, text: str, source: str, target: str
    ) -> AsyncIterator[str]: ...

    @abstractmethod
    async def ocr_translate(self, image_bytes: bytes, target: str) -> dict: ...
