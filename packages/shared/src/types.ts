export interface Language {
  code: string;
  name: string;
  nativeName: string;
}

export interface TranslateRequest {
  text: string;
  source: string;
  target: string;
}

export interface TranslateResponse {
  translated: string;
  source: string;
  target: string;
}

export interface OcrResponse {
  extracted: string;
  translated: string;
  target: string;
}

export interface SttResponse {
  text: string;
  language: string;
  translated?: string | null;
  target?: string | null;
}
