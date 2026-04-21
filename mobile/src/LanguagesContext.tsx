import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import type { Language } from "@translate/shared";
import { api } from "./api";

const Ctx = createContext<Language[]>([]);

export function LanguagesProvider({ children }: { children: ReactNode }) {
  const [languages, setLanguages] = useState<Language[]>([]);
  useEffect(() => {
    api.languages().then(setLanguages).catch(() => setLanguages([]));
  }, []);
  return <Ctx.Provider value={languages}>{children}</Ctx.Provider>;
}

export function useLanguages() {
  return useContext(Ctx);
}
