import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Translate — Gemma",
  description: "Text / OCR / Voice translation powered by Gemma",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
