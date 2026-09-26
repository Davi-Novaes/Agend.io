"use client";

import * as React from "react";
import { usePathname, useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";

import { useSession } from "@/lib/auth/session-context";
import { getSubscriptionGateStatus, getTenantProfile, TENANT_PROFILE_QUERY_KEY } from "@/lib/api/client";
import { decodeJwtRole } from "@/lib/auth/decode-jwt";
import { AppSidebar } from "@/components/layout/app-sidebar";
import { AppHeader } from "@/components/layout/app-header";
import { AppBackground } from "@/components/layout/app-background";
import { AssistantWidget } from "@/components/layout/assistant-widget";
import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar";
import { TenantThemeProvider } from "@/lib/tenant/tenant-theme-provider";
import { Button } from "@/components/ui/button";

// Unica rota liberada enquanto a assinatura de um plano pago ainda nao foi
// confirmada — precisa bater com a pagina que de fato tem o formulario de
// assinar/pagar (ver settings/account/plan/page.tsx, movida de settings/billing
// pra dentro de "Minha conta" — pedido explicito do usuario, 2026-09-05).
const BILLING_GATE_ALLOWED_PATH = "/settings/account/plan";

export default function AppLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const { session, isRestoringSession, logout } = useSession();

  React.useEffect(() => {
    if (!isRestoringSession && !session) {
      router.replace("/login");
    }
  }, [session, isRestoringSession, router]);

  const accessToken = session?.accessToken ?? "";
  const role = session ? decodeJwtRole(session.accessToken) : null;

  // Endpoint sem dado de billing de proposito: e o unico que Staff pode
  // chamar (a query completa de assinatura agora e Owner-only), e todo papel
  // precisa saber se o app deve ser bloqueado por pagamento pendente.
  const subscriptionQuery = useQuery({
    queryKey: ["billing", "subscription", "gate-status"],
    queryFn: () => getSubscriptionGateStatus(accessToken),
    enabled: Boolean(session),
  });

  // Mesma queryKey/queryFn de AppSidebar/AppHeader -- o React Query junta as
  // 3 chamadas numa unica requisicao (cache compartilhado pela key), entao
  // isto nao adiciona nenhum round-trip extra.
  const profileQuery = useQuery({
    queryKey: TENANT_PROFILE_QUERY_KEY,
    queryFn: () => getTenantProfile(accessToken),
    enabled: Boolean(session),
  });

  // Memoizado por VALOR (so muda quando primaryColorHex de fato muda), nao
  // recriado a cada render de AppLayout -- sem isto, TenantThemeProvider
  // recebia um objeto novo em toda renderizacao (mesmo sem a cor mudar,
  // ex.: qualquer refetch de subscriptionQuery/profileQuery em background),
  // e como seu efeito depende de `theme` por referencia ([theme] no useEffect),
  // isso disparava remove+reaplica das variaveis CSS no <html> sem necessidade
  // -- o que por sua vez reacionava o MutationObserver de AppBackground
  // (observa mutacao de "style") e recalculava o brilho do fundo ambiente a
  // toa. Bug real reportado pelo usuario: iluminacao "ficando mais forte do
  // nada" ao interagir com Aparencia (fonte/estilo de botao).
  const primaryColorHex = profileQuery.data?.primaryColorHex;
  const tenantTheme = React.useMemo(
    () => (primaryColorHex ? { primary: primaryColorHex, primaryForeground: "#ffffff" } : null),
    [primaryColorHex]
  );

  // Plano pago escolhido no onboarding, mas ainda sem cartao confirmado na
  // Asaas (Status continua Trialing ate o webhook de pagamento confirmar) —
  // bloqueia o resto do painel ate isso acontecer, redirecionando pra tela
  // de assinatura em vez do painel (P1-5, docs/AUTH_BILLING_SECURITY_AUDIT.md).
  // Plano Free nunca cai aqui: ActivateAsFree ja deixa o Status Active direto.
  const requiresPaymentBeforeAccess =
    subscriptionQuery.data !== undefined && subscriptionQuery.data.status !== "Active";
  // Staff nao acessa /settings/account/plan (Owner-only, ver BillingEndpoints) —
  // redirecionar pra la travaria a pessoa numa tela em branco/403. Em vez
  // disso mostra um aviso pedindo pra falar com o dono da conta.
  const isBlockedNonOwner = requiresPaymentBeforeAccess && role !== "Owner";

  React.useEffect(() => {
    if (requiresPaymentBeforeAccess && role === "Owner" && pathname !== BILLING_GATE_ALLOWED_PATH) {
      router.replace(BILLING_GATE_ALLOWED_PATH);
    }
  }, [requiresPaymentBeforeAccess, role, pathname, router]);

  // Enquanto isRestoringSession, ainda nao sabemos se ha um refresh token
  // valido no cookie — redirecionar aqui mandaria pro login uma sessao que
  // so nao terminou de ser restaurada ainda (ex.: logo apos um F5).
  if (isRestoringSession || !session) {
    return null;
  }

  if (isBlockedNonOwner) {
    return (
      <div className="flex min-h-svh flex-col items-center justify-center gap-3 p-6 text-center">
        <h1 className="text-lg font-medium">Assinatura pendente</h1>
        <p className="text-muted-foreground max-w-sm text-sm">
          O acesso está temporariamente bloqueado porque a assinatura do estabelecimento precisa ser regularizada.
          Fale com o administrador da conta.
        </p>
        <Button variant="outline" onClick={() => { logout(); router.replace("/login"); }}>
          Sair
        </Button>
      </div>
    );
  }

  // Evita um flash do conteudo protegido antes do redirect acima disparar.
  if (requiresPaymentBeforeAccess && role === "Owner" && pathname !== BILLING_GATE_ALLOWED_PATH) {
    return null;
  }

  const layout = (
    <SidebarProvider>
      <AppSidebar />
      {/* isolate: sem isto, o AppBackground (-z-10) some atras do proprio
          bg-background do <main> -- "position: relative" sozinho NAO cria um
          stacking context novo, entao um z-index negativo sobe pro contexto
          do documento inteiro em vez de ficar "so atras do conteudo deste
          <main>" (bug real, reproduzido e confirmado antes deste fix). */}
      <SidebarInset className="isolate">
        <AppBackground />
        <AppHeader />
        <div className="mx-auto flex w-full max-w-[1440px] flex-1 px-4 pt-5 pb-24 sm:px-6 sm:pt-8 md:pb-10 lg:px-8">
          {children}
        </div>
      </SidebarInset>
      <AssistantWidget />
    </SidebarProvider>
  );

  // So embrulha com TenantThemeProvider quando o dono realmente escolheu uma
  // cor propria em Configuracoes -> Marca -- sem isso o roxo padrao do design
  // system continua vindo direto do CSS (:root/.dark), preservando o tom mais
  // vivo do dark mode (DEFAULT_TENANT_THEME.primary e o roxo do tema claro,
  // mais escuro; forcar ele tambem no dark mode pra quem nao personalizou
  // nada seria uma regressao visual sem necessidade).
  return tenantTheme ? (
    <TenantThemeProvider theme={tenantTheme}>{layout}</TenantThemeProvider>
  ) : (
    layout
  );
}
