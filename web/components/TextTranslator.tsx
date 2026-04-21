"use client";

import { useState } from "react";
import type { Language } from "@translate/shared";
import { api } from "@/lib/api";
import { LanguagePicker } from "./LanguagePicker";

export function TextTranslator({ languages }: { languages: Language[] }) {
  const [source, setSource] = useState("auto");
  const [target, setTarget] = useState("en");
  const [text, setText] = useState("");
  const [output, setOutput] = useState("");
  const [busy, setBusy] = useState(false);

  async function run() {
    if (!text.trim()) return;
    setBusy(true);
    setOutput("");
    try {
      for await (const chunk of api.translateStream({ text, source, target })) {
        setOutput((p) => p + chunk);
      }
    } catch (e) {
      setOutput(`Error: ${(e as Error).message}`);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <LanguagePicker value={source} onChange={setSource} languages={languages} />
        <span className="text-slate-500">→</span>
        <LanguagePicker value={target} onChange={setTarget} languages={languages} excludeAuto />
      </div>
      <div className="grid gap-4 md:grid-cols-2">
        <textarea
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="Enter text…"
          className="min-h-[200px] w-full rounded-md border border-slate-300 bg-white p-3 text-sm dark:border-slate-700 dark:bg-slate-900"
        />
        <div className="min-h-[200px] w-full whitespace-pre-wrap rounded-md border border-slate-300 bg-slate-50 p-3 text-sm dark:border-slate-700 dark:bg-slate-900/50">
          {output || <span className="text-slate-400">Translation will appear here…</span>}
        </div>
      </div>
      <button
        onClick={run}
        disabled={busy}
        className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-500 disabled:opacity-50"
      >
        {busy ? "Translating…" : "Translate"}
      </button>
    </div>
  );
}
