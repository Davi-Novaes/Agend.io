import type { Metadata } from "next";
import Image from "next/image";
import { notFound } from "next/navigation";
import { Sparkles, TriangleAlert } from "lucide-react";

import { getTenantBySlug, resolveAssetUrl, ApiError, type TenantPublicProfile } from "@/lib/api/client";
import { TenantThemeProvider } from "@/lib/tenant/tenant-theme-provider";
import { DEFAULT_TENANT_THEME } from "@/lib/tenant/tenant-theme";
import { ReviewForm } from "@/components/public/review-form";
import { Card, CardContent } from "@/components/ui/card";

type PageProps = {
  params: Promise<{ slug: string }>;
  searchParams: Promise<{ appointmentId?: string }>;
};

async function loadTenant(slug: string): Promise<TenantPublicProfile | null> {
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

  return { title: tenant ? `Avaliar atendimento — ${tenant.name}` : "Estabelecimento nao encontrado" };
}

export default async function ReviewPage({ params, searchParams }: PageProps) {
  const { slug } = await params;
  const { appointmentId } = await searchParams;
  const tenant = await loadTenant(slug);

  // Estabelecimento inexistente/inativo: 404 generico de verdade (nao ha
  // marca nenhuma pra mostrar). Falta so o appointmentId (link mal
  // formado/incompleto) e um caso diferente — o tenant existe, entao da pra
  // mostrar uma tela com a marca dele em vez do 404 puro do Next (BL-28,
  // docs/BACKLOG.md).
  if (!tenant || !tenant.isActive) {
    notFound();
  }

  const theme = tenant.primaryColorHex
    ? { primary: tenant.primaryColorHex, primaryForeground: "#ffffff" }
    : DEFAULT_TENANT_THEME;

  return (
    <TenantThemeProvider theme={theme}>
      <div className="flex min-h-full flex-1 flex-col items-center justify-center px-4 py-14 sm:px-6">
        <div className="flex w-full max-w-md flex-col items-center gap-4">
          {tenant.logoUrl ? (
            <Image
              src={resolveAssetUrl(tenant.logoUrl)}
              alt={`Logo de ${tenant.name}`}
              width={64}
              height={64}
              className="rounded-2xl bg-background object-contain p-1.5 shadow-lg"
              unoptimized
            />
          ) : (
            <div className="flex size-16 items-center justify-center rounded-2xl bg-primary/10">
              <Sparkles className="text-primary size-7" strokeWidth={1.5} />
            </div>
          )}

          {appointmentId ? (
            <>
              <div className="text-center">
                <h1 className="text-xl font-semibold tracking-tight">Como foi seu atendimento?</h1>
                <p className="text-muted-foreground text-sm">{tenant.name}</p>
              </div>

              <Card className="w-full">
                <CardContent>
                  <ReviewForm tenantId={tenant.id} appointmentId={appointmentId} />
                </CardContent>
              </Card>
            </>
          ) : (
            <>
              <div className="text-center">
                <h1 className="text-xl font-semibold tracking-tight">{tenant.name}</h1>
              </div>

              <Card className="w-full">
                <CardContent className="flex flex-col items-center gap-3 py-8 text-center">
                  <TriangleAlert className="text-muted-foreground size-8" strokeWidth={1.5} aria-hidden="true" />
                  <div>
                    <p className="font-medium">Link de avaliacao incompleto</p>
                    <p className="text-muted-foreground text-sm">
                      Esse link nao informa qual atendimento avaliar. Use o link que voce recebeu por
                      e-mail ou WhatsApp apos o atendimento.
                    </p>
                  </div>
                </CardContent>
              </Card>
            </>
          )}
        </div>
      </div>
    </TenantThemeProvider>
  );
}
