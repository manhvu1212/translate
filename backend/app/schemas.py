from pydantic import BaseModel, Field


class TranslateRequest(BaseModel):
    text: str = Field(min_length=1, max_length=20000)
    source: str = "auto"
    target: str = "en"


class TranslateResponse(BaseModel):
    translated: str
    source: str
    target: str


class OcrResponse(BaseModel):
    extracted: str
    translated: str
    target: str


class SttResponse(BaseModel):
    text: str
    language: str
    translated: str | None = None
    target: str | None = None


class LanguagesResponse(BaseModel):
    languages: list[dict]
