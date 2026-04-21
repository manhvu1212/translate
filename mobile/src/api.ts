import Constants from "expo-constants";
import { TranslateClient, type OcrResponse, type SttResponse } from "@translate/shared";

const apiUrl =
  (Constants.expoConfig?.extra as { apiUrl?: string } | undefined)?.apiUrl ??
  "http://localhost:8000";

export const api = new TranslateClient(apiUrl);

// React Native FormData accepts { uri, name, type } directly — no Blob needed.
// Using Blob in RN fails with "Creating blobs from ArrayBuffer ... not supported".

export async function ocrFromUri(
  uri: string,
  mimeType: string,
  target: string,
): Promise<OcrResponse> {
  const form = new FormData();
  const ext = mimeType.split("/")[1] ?? "jpg";
  form.append("image", { uri, name: `image.${ext}`, type: mimeType } as unknown as Blob);
  form.append("target", target);
  const r = await fetch(`${apiUrl}/ocr`, { method: "POST", body: form });
  if (!r.ok) throw new Error(`ocr ${r.status}`);
  return r.json();
}

export async function sttFromUri(
  uri: string,
  opts: { language?: string; target?: string } = {},
): Promise<SttResponse> {
  const form = new FormData();
  const m = uri.match(/\.([a-zA-Z0-9]+)(?:\?|$)/);
  const ext = m?.[1]?.toLowerCase() ?? "m4a";
  const mime = ext === "wav" ? "audio/wav" : ext === "mp3" ? "audio/mpeg" : "audio/m4a";
  form.append("audio", {
    uri,
    name: `recording.${ext}`,
    type: mime,
  } as unknown as Blob);
  form.append("language", opts.language ?? "auto");
  if (opts.target) form.append("target", opts.target);
  const r = await fetch(`${apiUrl}/stt`, { method: "POST", body: form });
  if (!r.ok) throw new Error(`stt ${r.status}`);
  return r.json();
}
