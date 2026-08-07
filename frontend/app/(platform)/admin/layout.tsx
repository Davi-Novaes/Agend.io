import type { Metadata } from "next";
import type { ReactNode } from "react";

// Sem SEO de proposito: painel interno do Super Admin, nunca indexado.
export const metadata: Metadata = {
  title: "Agendio Platform",
  robots: { index: false, follow: false },
};

export default function PlatformAdminLayout({ children }: { children: ReactNode }) {
  return children;
}
