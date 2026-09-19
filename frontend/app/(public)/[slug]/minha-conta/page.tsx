import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, CalendarPlus, Sparkles } from "lucide-react";

import { ApiError, getTenantBySlug, resolveAssetUrl, type TenantPublicProfile } from "@/lib/api/client";
import { DEFAULT_TENANT_THEME } from "@/lib/tenant/tenant-theme";
import { TenantThemeProvider } from "@/lib/tenant/tenant-theme-provider";
import { CustomerPortal } from "@/components/public/customer-portal";
import { Button } from "@/components/ui/button";

type PageProps = { params: Promise<{ slug: string }> };

async function loadTenant(slug: string): Promise<TenantPublicProfile | null> {
  try {
    return await getTenantBySlug(slug);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null;
    throw error;
  }
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { slug } = await params;
  const tenant = await loadTenant(slug);
  return {
    title: tenant ? `Minha conta | ${tenant.name}` : "Estabelecimento não encontrado",
    robots: { index: false, follow: false },
  };
}

export default async function CustomerPortalPage({ params }: PageProps) {
  const { slug } = await params;
  const tenant = await loadTenant(slug);
  if (!tenant || !tenant.isActive || !tenant.publicPageEnabled) notFound();

  const theme = tenant.primaryColorHex
    ? { primary: tenant.primaryColorHex, primaryForeground: "#ffffff" }
    : DEFAULT_TENANT_THEME;

  return (
    <TenantThemeProvider theme={theme}>
      <div className="min-h-full bg-background text-foreground">
        <header className="bg-background/92 sticky top-0 z-50 border-b backdrop-blur-xl">
          <div className="mx-auto flex h-16 w-full max-w-5xl items-center gap-3 px-4 sm:px-6">
            <Link href={`/${slug}`} className="flex min-w-0 items-center gap-2.5" aria-label={`Voltar para ${tenant.name}`}>
              {tenant.logoUrl ? <Image src={resolveAssetUrl(tenant.logoUrl)} alt="" width={36} height={36} className="size-9 rounded-xl bg-card object-contain p-0.5 ring-1 ring-border" unoptimized /> : <span className="bg-primary text-primary-foreground flex size-9 items-center justify-center rounded-xl"><Sparkles className="size-4" /></span>}
              <span className="truncate font-semibold tracking-tight">{tenant.name}</span>
            </Link>
            <Button variant="ghost" className="ml-auto hidden sm:inline-flex" asChild><Link href={`/${slug}`}><ArrowLeft className="size-4" />Voltar para a página</Link></Button>
            <Button asChild><Link href={`/${slug}#agendar`}><CalendarPlus className="size-4" />Agendar</Link></Button>
          </div>
        </header>

        <main className="relative isolate overflow-hidden px-4 py-12 sm:px-6 sm:py-16">
          <div aria-hidden className="absolute inset-x-0 top-0 -z-10 h-72 bg-gradient-to-b from-primary/10 to-transparent" />
          <div className="mx-auto w-full max-w-5xl">
            <CustomerPortal tenantId={tenant.id} slug={slug} tenantName={tenant.name} />
          </div>
        </main>
      </div>
    </TenantThemeProvider>
  );
}
