"use client";

import { useEffect, useState } from "react";
import type { Language } from "@translate/shared";
import { api } from "@/lib/api";
import { TextTranslator } from "@/components/TextTranslator";
import { OcrTranslator } from "@/components/OcrTranslator";
import { VoiceTranslator } from "@/components/VoiceTranslator";

type Tab = "text" | "ocr" | "voice";

const TABS: { id: Tab; label: string }[] = [
  { id: "text", label: "Text" },
  { id: "ocr", label: "Image (OCR)" },
  { id: "voice", label: "Voice" },
];

export default function Home() {
  const [tab, setTab] = useState<Tab>("text");
  const [languages, setLanguages] = useState<Language[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api
      .languages()
      .then(setLanguages)
      .catch((e) => setError((e as Error).message));
  }, []);

  return (
    <main className="mx-auto max-w-4xl px-4 py-8">
      <header className="mb-6">
        <h1 className="text-2xl font-bold">Translate</h1>
        <p className="text-sm text-slate-500">Powered by Gemma — text, image OCR, and voice.</p>
      </header>

      {error && (
        <div className="mb-4 rounded-md border border-red-300 bg-red-50 p-3 text-sm text-red-800 dark:border-red-800 dark:bg-red-950/40 dark:text-red-200">
          API unreachable: {error}
        </div>
      )}

      <div className="mb-6 flex gap-1 rounded-lg bg-slate-200 p-1 dark:bg-slate-800">
        {TABS.map((t) => (
          <button
            key={t.id}
            onClick={() => setTab(t.id)}
            className={`flex-1 rounded-md px-3 py-2 text-sm font-medium transition ${
              tab === t.id
                ? "bg-white shadow-sm dark:bg-slate-900"
                : "text-slate-600 hover:text-slate-900 dark:text-slate-400 dark:hover:text-slate-200"
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {languages.length > 0 && (
        <>
          {tab === "text" && <TextTranslator languages={languages} />}
          {tab === "ocr" && <OcrTranslator languages={languages} />}
          {tab === "voice" && <VoiceTranslator languages={languages} />}
        </>
      )}
    </main>
  );
}
