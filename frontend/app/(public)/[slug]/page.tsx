import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { CalendarClock } from "lucide-react";
import { getTenantBySlug, ApiError } from "@/lib/api/client";

type PageProps = {
  params: Promise<{ slug: string }>;
};

async function loadTenant(slug: string) {
  try {
    return await getTenantBySlug(slug);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      return null;
    }
    throw error;
  }
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { slug } = await params;
  const tenant = await loadTenant(slug);

  return {
    title: tenant ? `${tenant.name} — Agendio` : "Estabelecimento nao encontrado",
  };
}

// SSR de proposito (nao 'use client'): esta e a pagina publica indexavel do
// tenant — precisa de HTML pronto no primeiro request para SEO.
export default async function TenantPortalPage({ params }: PageProps) {
  const { slug } = await params;
  const tenant = await loadTenant(slug);

  if (!tenant || !tenant.isActive) {
    notFound();
  }

  return (
    <main className="flex min-h-full flex-1 flex-col items-center justify-center gap-4 p-4 text-center">
      <span className="bg-accent text-accent-foreground flex size-14 items-center justify-center rounded-2xl">
        <CalendarClock className="size-7" strokeWidth={1.75} />
      </span>
      <div className="space-y-1.5">
        <h1 className="text-2xl font-semibold tracking-tight">{tenant.name}</h1>
        <p className="text-muted-foreground text-sm">
          Agendamento online chegando em breve.
        </p>
      </div>
    </main>
  );
}
