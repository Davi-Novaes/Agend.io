"use client";

import { useSession } from "@/lib/auth/session-context";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";

export default function DashboardPage() {
  const { session } = useSession();

  if (!session) {
    return null;
  }

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-1 flex-col">
      <Card>
        <CardHeader>
          <CardTitle>Bem-vindo de volta</CardTitle>
          <CardDescription>
            Sessão válida até {new Date(session.expiresAtUtc).toLocaleTimeString("pt-BR")}.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <p className="text-muted-foreground text-sm">
            Use o menu à esquerda para navegar entre os módulos. Um painel com indicadores do seu negócio chega em
            breve.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
