"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { Eye, FileText, Globe2, Info, LayoutDashboard, Palette, Sparkles } from "lucide-react";

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
  { href: "/settings/branding", label: "Visão geral", icon: LayoutDashboard },
  { href: "/settings/branding/appearance", label: "Aparência", icon: Palette },
  { href: "/settings/branding/content", label: "Conteúdo", icon: FileText },
  { href: "/settings/branding/info", label: "Informações", icon: Info },
  { href: "/settings/branding/public", label: "Página pública", icon: Globe2 },
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
    <div className="flex w-full flex-1 flex-col gap-5">
      <div className="relative overflow-hidden rounded-2xl border bg-gradient-to-br from-primary/12 via-card to-card p-5 sm:p-6">
        <div aria-hidden className="absolute -right-10 -top-12 size-40 rounded-full bg-primary/10 blur-3xl" />
        <div className="relative flex items-start gap-4">
          <div className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-primary text-primary-foreground shadow-lg shadow-primary/20">
            <Sparkles className="size-5" />
          </div>
          <div>
            <p className="text-xs font-semibold tracking-[0.18em] text-primary uppercase">Identidade da empresa</p>
            <h2 className="mt-1 text-xl font-semibold tracking-tight">Sua marca, do seu jeito</h2>
            <p className="mt-1 max-w-2xl text-sm leading-relaxed text-muted-foreground">
              Ajuste aparência, textos e informações enquanto acompanha o resultado na prévia ao lado.
            </p>
          </div>
        </div>
      </div>

      {!isOwner && (
        <p className="text-muted-foreground text-sm">Somente o administrador da conta pode alterar estas configurações.</p>
      )}

      <nav
        aria-label="Sub-navegação de Marca"
        className="flex w-full max-w-full items-center gap-1 overflow-x-auto rounded-xl border bg-card/70 p-1 shadow-sm backdrop-blur"
      >
        {BRANDING_TABS.map((tab) => {
          const isActive = pathname === tab.href;
          const Icon = tab.icon;
          return (
            <Link
              key={tab.href}
              href={tab.href}
              aria-current={isActive ? "page" : undefined}
              className={cn(
                "inline-flex shrink-0 items-center gap-2 rounded-lg border border-transparent px-3 py-2 text-sm font-medium whitespace-nowrap transition-all",
                isActive
                  ? "border-border bg-background text-foreground shadow-sm"
                  : "text-muted-foreground hover:bg-muted/60 hover:text-foreground"
              )}
            >
              <Icon className={cn("size-4", isActive && "text-primary")} aria-hidden="true" />
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
          <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1fr)_390px]">
            <div className="min-w-0">
              <fieldset disabled={!isOwner} className="contents">
                {children}
              </fieldset>
            </div>
            <div className="sticky top-6 hidden xl:block">
              <LivePreviewPanel />
            </div>
          </div>
        </BrandingDraftProvider>
      )}
    </div>
  );
}
