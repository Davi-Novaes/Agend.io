import type { Metadata } from "next";

// Sem SEO de proposito: painel interno do Super Admin, nunca indexado.
export const metadata: Metadata = {
  title: "Agendio Platform",
  robots: { index: false, follow: false },
};

export default function PlatformAdminPage() {
  return (
    <main className="flex min-h-full flex-1 items-center justify-center p-4">
      <p className="text-muted-foreground text-sm">
        Painel Super Admin — chega no Sprint 6 do roadmap.
      </p>
    </main>
  );
}
