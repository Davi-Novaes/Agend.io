"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { Eye } from "lucide-react";

import { cn } from "@/lib/utils";
import { useSession } from "@/lib/auth/session-context";
import { decodeJwtRole } from "@/lib/auth/decode-jwt";
import { getTenantProfile, TENANT_PROFILE_QUERY_KEY } from "@/lib/api/client";
import { BrandingDraftProvider } from "@/components/settings/branding/branding-draft-context";
import { LivePreviewPanel } from "@/components/settings/branding/live-preview-panel";
import { Skeleton } from "@/components/ui/skeleton";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from "@/components/ui/sheet";

const BRANDING_TABS = [
  { href: "/settings/branding", label: "Visão geral" },
  { href: "/settings/branding/appearance", label: "Aparência" },
  { href: "/settings/branding/content", label: "Conteúdo" },
  { href: "/settings/branding/info", label: "Informações" },
  { href: "/settings/branding/public", label: "Página pública" },
];

// Marca funciona como um mini-builder do portal publico: sub-navegacao
// interna (em vez de uma unica pagina gigante, ver CLAUDE.md) + uma previa ao
// vivo que reage ao estado de edicao em memoria (BrandingDraftProvider),
// nao ao dado ja salvo -- e o mesmo motivo de nao reusar TenantThemeProvider
// aqui: aquele muda a cor --primary do documento inteiro (usado so na pagina
// publica), o que recolorira o painel admin inteiro enquanto o dono so esta
// testando uma cor. LivePreviewPanel e um componente proprio, isolado.
//
// Um UNICO BrandingDraftProvider embrulha o layout inteiro (nav + abas +
// previa desktop + previa mobile no Sheet) -- ter um provider por previa
// criaria dois estados independentes, e editar numa aba nao refletiria na
// previa do Sheet mobile (ou vice-versa).
export default function BrandingLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const { session } = useSession();
  const accessToken = session?.accessToken ?? "";
  const role = session ? decodeJwtRole(session.accessToken) : null;
  const isOwner = role === "Owner";

  const profileQuery = useQuery({
    queryKey: TENANT_PROFILE_QUERY_KEY,
    queryFn: () => getTenantProfile(accessToken),
    enabled: Boolean(session),
  });

  return (
    <div className="flex w-full flex-1 flex-col gap-6">
      <div>
        <h1 className="text-lg font-semibold">Marca</h1>
        <p className="text-muted-foreground text-sm">Personalize a identidade visual e o conteúdo do seu portal público.</p>
      </div>

      {!isOwner && (
        <p className="text-muted-foreground text-sm">Somente o administrador da conta pode alterar estas configurações.</p>
      )}

      <nav
        aria-label="Sub-navegação de Marca"
        className="bg-muted text-muted-foreground -mx-1 flex w-fit max-w-full items-center gap-0.5 overflow-x-auto rounded-lg p-[3px]"
      >
        {BRANDING_TABS.map((tab) => {
          const isActive = pathname === tab.href;
          return (
            <Link
              key={tab.href}
              href={tab.href}
              aria-current={isActive ? "page" : undefined}
              className={cn(
                "inline-flex shrink-0 items-center gap-1.5 rounded-md border border-transparent px-2.5 py-1 text-sm font-medium whitespace-nowrap transition-colors",
                isActive ? "bg-background text-foreground shadow-sm" : "hover:text-foreground"
              )}
            >
              {tab.label}
            </Link>
          );
        })}
      </nav>

      {profileQuery.isLoading || !profileQuery.data ? (
        <div className="grid gap-6 lg:grid-cols-[1fr_360px]">
          <div className="flex flex-col gap-4">
            <Skeleton className="h-40 w-full" />
            <Skeleton className="h-56 w-full" />
          </div>
          <Skeleton className="hidden h-96 w-full lg:block" />
        </div>
      ) : (
        <BrandingDraftProvider profile={profileQuery.data}>
          <div className="flex justify-end lg:hidden">
            <Sheet>
              <SheetTrigger asChild>
                <Button variant="outline" size="sm">
                  <Eye className="size-4" />
                  Ver prévia
                </Button>
              </SheetTrigger>
              <SheetContent side="bottom" className="h-[85vh] w-full max-w-none">
                <SheetHeader>
                  <SheetTitle>Pré-visualização</SheetTitle>
                </SheetHeader>
                <div className="overflow-y-auto px-4 pb-4">
                  <LivePreviewPanel />
                </div>
              </SheetContent>
            </Sheet>
          </div>
          <div className="grid items-start gap-6 lg:grid-cols-[1fr_360px]">
            <div className="min-w-0">
              <fieldset disabled={!isOwner} className="contents">
                {children}
              </fieldset>
            </div>
            <div className="sticky top-6 hidden lg:block">
              <LivePreviewPanel />
            </div>
          </div>
        </BrandingDraftProvider>
      )}
    </div>
  );
}
