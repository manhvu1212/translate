import type {
  Language,
  OcrResponse,
  SttResponse,
  TranslateRequest,
  TranslateResponse,
} from "./types";

export class TranslateClient {
  constructor(private baseUrl: string) {
    this.baseUrl = baseUrl.replace(/\/$/, "");
  }

  async languages(): Promise<Language[]> {
    const r = await fetch(`${this.baseUrl}/languages`);
    if (!r.ok) throw new Error(`languages ${r.status}`);
    const data = (await r.json()) as { languages: Language[] };
    return data.languages;
  }

  async translate(req: TranslateRequest): Promise<TranslateResponse> {
    const r = await fetch(`${this.baseUrl}/translate`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(req),
    });
    if (!r.ok) throw new Error(`translate ${r.status}`);
    return r.json();
  }

  async *translateStream(req: TranslateRequest): AsyncGenerator<string> {
    const r = await fetch(`${this.baseUrl}/translate/stream`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(req),
    });
    if (!r.ok || !r.body) throw new Error(`stream ${r.status}`);
    const reader = r.body.getReader();
    const decoder = new TextDecoder();
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      if (value) yield decoder.decode(value, { stream: true });
    }
  }

  async ocr(image: Blob, target: string): Promise<OcrResponse> {
    const form = new FormData();
    form.append("image", image, (image as File).name ?? "image.png");
    form.append("target", target);
    const r = await fetch(`${this.baseUrl}/ocr`, { method: "POST", body: form });
    if (!r.ok) throw new Error(`ocr ${r.status}`);
    return r.json();
  }

  async stt(
    audio: Blob,
    opts: { language?: string; target?: string; filename?: string } = {},
  ): Promise<SttResponse> {
    const form = new FormData();
    form.append("audio", audio, opts.filename ?? (audio as File).name ?? "audio.wav");
    form.append("language", opts.language ?? "auto");
    if (opts.target) form.append("target", opts.target);
    const r = await fetch(`${this.baseUrl}/stt`, { method: "POST", body: form });
    if (!r.ok) throw new Error(`stt ${r.status}`);
    return r.json();
  }
}
