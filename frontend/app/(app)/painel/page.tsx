"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useSession } from "@/lib/auth/session-context";
import { Logo } from "@/components/logo";
import { Button } from "@/components/ui/button";

export default function DashboardPage() {
  const router = useRouter();
  const { session, logout } = useSession();

  React.useEffect(() => {
    if (!session) {
      router.replace("/login");
    }
  }, [session, router]);

  if (!session) {
    return null;
  }

  return (
    <main className="flex min-h-full flex-1 flex-col items-center justify-center gap-4 p-4">
      <Logo />
      <p className="text-muted-foreground text-sm">
        Sessao valida ate {new Date(session.expiresAtUtc).toLocaleTimeString("pt-BR")}.
      </p>
      <div className="flex flex-wrap justify-center gap-2">
        <Button variant="outline" asChild>
          <Link href="/agenda">Agenda</Link>
        </Button>
        <Button variant="outline" asChild>
          <Link href="/clientes">Clientes</Link>
        </Button>
        <Button variant="outline" asChild>
          <Link href="/servicos">Servicos</Link>
        </Button>
        <Button variant="outline" asChild>
          <Link href="/recursos">Recursos</Link>
        </Button>
        <Button variant="outline" asChild>
          <Link href="/financeiro">Financeiro</Link>
        </Button>
        <Button variant="outline" asChild>
          <Link href="/settings/branding">Marca</Link>
        </Button>
        <Button variant="outline" asChild>
          <Link href="/settings/team">Equipe</Link>
        </Button>
        <Button variant="outline" asChild>
          <Link href="/settings/billing">Plano</Link>
        </Button>
        <Button variant="outline" asChild>
          <Link href="/settings/security">Segurança</Link>
        </Button>
      </div>
      <Button
        variant="outline"
        onClick={() => {
          logout();
          router.replace("/login");
        }}
      >
        Sair
      </Button>
    </main>
  );
}
