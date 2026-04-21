"use client";

import type { Language } from "@translate/shared";

interface Props {
  value: string;
  onChange: (code: string) => void;
  languages: Language[];
  excludeAuto?: boolean;
}

export function LanguagePicker({ value, onChange, languages, excludeAuto }: Props) {
  const list = excludeAuto ? languages.filter((l) => l.code !== "auto") : languages;
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className="rounded-md border border-slate-300 bg-white px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-900"
    >
      {list.map((l) => (
        <option key={l.code} value={l.code}>
          {l.nativeName} ({l.name})
        </option>
      ))}
    </select>
  );
}
