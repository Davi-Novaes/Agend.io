"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";

import { cn } from "@/lib/utils";
import { useSession } from "@/lib/auth/session-context";
import { decodeJwtRole } from "@/lib/auth/decode-jwt";

// Empresa/Plano ficam de fora do array pra Staff de proposito (nao so
// escondidas por CSS) -- backend ja rejeita ambas com 403 pra quem nao e
// Owner (GET/PUT /api/tenants/company-info, GetMySubscription), entao nem
// faz sentido renderizar a aba. Pedido explicito do usuario (2026-09-05):
// "membro comum deve ter acesso apenas as informacoes de controle dos
// clientes do site marca etc [...] informacoes de plano [e empresa] nao
// pode ter acesso".
const ACCOUNT_TABS = [
  { href: "/settings/account", label: "Perfil", ownerOnly: false },
  { href: "/settings/account/company", label: "Empresa", ownerOnly: true },
  { href: "/settings/account/plan", label: "Plano", ownerOnly: true },
];

export default function AccountLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const { session } = useSession();
  const isOwner = session ? decodeJwtRole(session.accessToken) === "Owner" : false;

  const visibleTabs = ACCOUNT_TABS.filter((tab) => !tab.ownerOnly || isOwner);

  return (
    <div className="flex w-full flex-1 flex-col gap-6">
      <div>
        <h1 className="text-lg font-semibold">Minha conta</h1>
        <p className="text-muted-foreground text-sm">Seus dados pessoais e, se você for o administrador, os da empresa e do plano.</p>
      </div>

      <nav
        aria-label="Sub-navegação de Minha conta"
        className="bg-muted text-muted-foreground -mx-1 flex w-fit max-w-full items-center gap-0.5 overflow-x-auto rounded-lg p-[3px]"
      >
        {visibleTabs.map((tab) => {
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

      {children}
    </div>
  );
}
