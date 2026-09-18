import type { Metadata } from "next";
import { Plus_Jakarta_Sans, Geist_Mono } from "next/font/google";
import { Providers } from "@/app/providers";
import "./globals.css";

const plusJakartaSans = Plus_Jakarta_Sans({
  variable: "--font-plus-jakarta-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

// Fallback so serve pra dev local sem NEXT_PUBLIC_API_URL configurado -- em
// producao aponta pro mesmo dominio publico (Caddy serve frontend e /api/*
// do mesmo host, ver PUBLIC_ORIGIN em infra/docker-compose.prod.yml).
export const SITE_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:3000";

export const metadata: Metadata = {
  metadataBase: new URL(SITE_URL),
  title: "AgendioBR",
  description: "Plataforma de gestao para negocios baseados em agendamento.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      lang="pt-BR"
      className={`${plusJakartaSans.variable} ${geistMono.variable} h-full antialiased`}
      // next-themes escreve style={colorScheme} direto no <html> apos
      // hidratar (evita flash de tema errado) — mismatch esperado entre
      // servidor e cliente, suprimir e a recomendacao oficial da lib.
      suppressHydrationWarning
    >
      <body className="min-h-full flex flex-col">
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
