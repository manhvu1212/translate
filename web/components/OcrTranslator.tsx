"use client";

import { useRef, useState } from "react";
import type { Language } from "@translate/shared";
import { api } from "@/lib/api";
import { LanguagePicker } from "./LanguagePicker";

export function OcrTranslator({ languages }: { languages: Language[] }) {
  const [target, setTarget] = useState("en");
  const [preview, setPreview] = useState<string | null>(null);
  const [extracted, setExtracted] = useState("");
  const [translated, setTranslated] = useState("");
  const [busy, setBusy] = useState(false);
  const fileRef = useRef<HTMLInputElement>(null);

  async function onFile(f: File) {
    setPreview(URL.createObjectURL(f));
    setExtracted("");
    setTranslated("");
    setBusy(true);
    try {
      const r = await api.ocr(f, target);
      setExtracted(r.extracted);
      setTranslated(r.translated);
    } catch (e) {
      setExtracted(`Error: ${(e as Error).message}`);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <span className="text-sm text-slate-500">Translate to</span>
        <LanguagePicker value={target} onChange={setTarget} languages={languages} excludeAuto />
        <button
          onClick={() => fileRef.current?.click()}
          className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-500"
        >
          Choose image
        </button>
        <input
          ref={fileRef}
          type="file"
          accept="image/*"
          className="hidden"
          onChange={(e) => {
            const f = e.target.files?.[0];
            if (f) onFile(f);
          }}
        />
      </div>
      {preview && (
        <img
          src={preview}
          alt="preview"
          className="max-h-64 rounded-md border border-slate-300 dark:border-slate-700"
        />
      )}
      <div className="grid gap-4 md:grid-cols-2">
        <div className="min-h-[160px] whitespace-pre-wrap rounded-md border border-slate-300 bg-slate-50 p-3 text-sm dark:border-slate-700 dark:bg-slate-900/50">
          <div className="mb-1 text-xs font-semibold text-slate-500">Extracted</div>
          {busy && !extracted ? "Reading…" : extracted || <span className="text-slate-400">—</span>}
        </div>
        <div className="min-h-[160px] whitespace-pre-wrap rounded-md border border-slate-300 bg-slate-50 p-3 text-sm dark:border-slate-700 dark:bg-slate-900/50">
          <div className="mb-1 text-xs font-semibold text-slate-500">Translated</div>
          {busy && !translated ? "Translating…" : translated || <span className="text-slate-400">—</span>}
        </div>
      </div>
    </div>
  );
}
