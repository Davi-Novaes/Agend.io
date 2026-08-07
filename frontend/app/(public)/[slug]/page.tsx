import type { Metadata } from "next";
import Image from "next/image";
import { notFound } from "next/navigation";
import { getTenantBySlug, resolveAssetUrl, ApiError } from "@/lib/api/client";
import { TenantThemeProvider } from "@/lib/tenant/tenant-theme-provider";
import { DEFAULT_TENANT_THEME } from "@/lib/tenant/tenant-theme";
import { BookingFlow } from "@/components/public/booking-flow";

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
    title: tenant ? `Agendar em ${tenant.name}` : "Estabelecimento nao encontrado",
    description: tenant ? `Agende um horario em ${tenant.name} pela internet, em poucos passos.` : undefined,
  };
}

// SSR de proposito (nao 'use client'): esta e a pagina publica indexavel do
// tenant — precisa de HTML pronto no primeiro request para SEO. A parte
// interativa (fluxo de agendamento) vive num Client Component a parte.
export default async function TenantPortalPage({ params }: PageProps) {
  const { slug } = await params;
  const tenant = await loadTenant(slug);

  if (!tenant || !tenant.isActive) {
    notFound();
  }

  // A cor customizada so foi aceita ao salvar (settings/branding) se tivesse
  // contraste AA suficiente contra branco — por isso o texto sobre ela pode
  // ser sempre branco aqui, sem recalcular nada.
  const theme = tenant.primaryColorHex
    ? { primary: tenant.primaryColorHex, primaryForeground: "#ffffff" }
    : DEFAULT_TENANT_THEME;

  return (
    <TenantThemeProvider theme={theme}>
      <main className="flex min-h-full flex-1 flex-col items-center gap-8 p-4 py-10 sm:p-10">
        <header className="flex flex-col items-center gap-3 text-center">
          {tenant.logoUrl && (
            <Image
              src={resolveAssetUrl(tenant.logoUrl)}
              alt={`Logo de ${tenant.name}`}
              width={72}
              height={72}
              className="rounded-2xl object-contain"
              unoptimized
            />
          )}
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">{tenant.name}</h1>
            <p className="text-muted-foreground text-sm">Agende seu horario online</p>
          </div>
        </header>

        <BookingFlow tenantId={tenant.id} />
      </main>
    </TenantThemeProvider>
  );
}
