"use client";

import { useRef, useState } from "react";
import type { Language } from "@translate/shared";
import { api } from "@/lib/api";
import { LanguagePicker } from "./LanguagePicker";

export function VoiceTranslator({ languages }: { languages: Language[] }) {
  const [source, setSource] = useState("auto");
  const [target, setTarget] = useState("en");
  const [recording, setRecording] = useState(false);
  const [busy, setBusy] = useState(false);
  const [transcript, setTranscript] = useState("");
  const [translated, setTranslated] = useState("");
  const recorderRef = useRef<MediaRecorder | null>(null);
  const chunksRef = useRef<Blob[]>([]);

  async function start() {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    const rec = new MediaRecorder(stream);
    chunksRef.current = [];
    rec.ondataavailable = (e) => e.data.size && chunksRef.current.push(e.data);
    rec.onstop = async () => {
      stream.getTracks().forEach((t) => t.stop());
      const blob = new Blob(chunksRef.current, { type: "audio/webm" });
      await send(blob);
    };
    rec.start();
    recorderRef.current = rec;
    setRecording(true);
  }

  function stop() {
    recorderRef.current?.stop();
    setRecording(false);
  }

  async function send(blob: Blob) {
    setBusy(true);
    setTranscript("");
    setTranslated("");
    try {
      const r = await api.stt(blob, {
        language: source,
        target,
        filename: "recording.webm",
      });
      setTranscript(r.text);
      setTranslated(r.translated ?? "");
    } catch (e) {
      setTranscript(`Error: ${(e as Error).message}`);
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
      <button
        onClick={recording ? stop : start}
        disabled={busy}
        className={`rounded-md px-4 py-2 text-sm font-medium text-white disabled:opacity-50 ${
          recording ? "bg-red-600 hover:bg-red-500" : "bg-indigo-600 hover:bg-indigo-500"
        }`}
      >
        {recording ? "■ Stop" : busy ? "Processing…" : "● Record"}
      </button>
      <div className="grid gap-4 md:grid-cols-2">
        <div className="min-h-[120px] whitespace-pre-wrap rounded-md border border-slate-300 bg-slate-50 p-3 text-sm dark:border-slate-700 dark:bg-slate-900/50">
          <div className="mb-1 text-xs font-semibold text-slate-500">Transcript</div>
          {transcript || <span className="text-slate-400">—</span>}
        </div>
        <div className="min-h-[120px] whitespace-pre-wrap rounded-md border border-slate-300 bg-slate-50 p-3 text-sm dark:border-slate-700 dark:bg-slate-900/50">
          <div className="mb-1 text-xs font-semibold text-slate-500">Translated</div>
          {translated || <span className="text-slate-400">—</span>}
        </div>
      </div>
    </div>
  );
}
