"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";

import { getSecurityActivityLog, type SecurityActivityEntry } from "@/lib/api/client";
import { usePlatformSession } from "@/lib/auth/platform-session-context";
import { parseUserAgent } from "@/lib/user-agent";
import { AdminNav } from "@/components/platform/admin-nav";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { ShieldAlert } from "lucide-react";

const EVENT_LABELS: Record<string, string> = {
  LoginFailed: "Login falhou",
  AccountLocked: "Conta bloqueada",
  LoginBlocked: "Login bloqueado (e-mail nao confirmado)",
  LoginMfaChallengeIssued: "Desafio MFA emitido",
  LoginSucceeded: "Login bem-sucedido",
  RegistrationConfirmed: "Conta criada",
};

type SuccessFilter = "all" | "success" | "failure";

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString("pt-BR", { dateStyle: "short", timeStyle: "medium" });
}

const countryNameFormatter = typeof Intl.DisplayNames === "function" ? new Intl.DisplayNames(["pt-BR"], { type: "region" }) : null;

// "BR" -> bandeira via Regional Indicator Symbols (cada letra vira um
// codepoint Unicode que o navegador desenha como bandeira) -- sem lib nem
// arquivo de imagem, e o mesmo truque que WhatsApp/iOS usam.
function countryFlag(countryCode: string): string {
  return String.fromCodePoint(...[...countryCode.toUpperCase()].map((letter) => 127397 + letter.charCodeAt(0)));
}

// Cidade/regiao so vem preenchido se a Cloudflare tiver "Add visitor
// location headers" habilitado na zona (Managed Transforms) -- sem isso, cai
// de volta pro pais (ja funcionava antes). Nao e bug de leitura, e ausencia
// de configuracao no lado da Cloudflare.
function formatLocation(entry: Pick<SecurityActivityEntry, "countryCode" | "region" | "city">): string {
  const place = [entry.city, entry.region && entry.region !== entry.city ? entry.region : null].filter(Boolean).join(", ");
  if (!entry.countryCode) return place || "—";
  const flag = countryFlag(entry.countryCode);
  if (place) return `${flag} ${place}`;
  return `${flag} ${countryNameFormatter?.of(entry.countryCode) ?? entry.countryCode}`;
}

export default function PlatformActivityPage() {
  const router = useRouter();
  const { session } = usePlatformSession();

  const [search, setSearch] = React.useState("");
  const [successFilter, setSuccessFilter] = React.useState<SuccessFilter>("all");

  React.useEffect(() => {
    if (!session) {
      router.replace("/admin/login");
    }
  }, [session, router]);

  const accessToken = session?.accessToken ?? "";

  const activityQuery = useQuery({
    queryKey: ["platform", "security-activity"],
    queryFn: () => getSecurityActivityLog(accessToken),
    enabled: Boolean(session),
    // Painel de monitoramento -- refetch periodico em vez de exigir F5 manual
    // pra ver atividade recente (evento de seguranca perde valor se atrasado).
    refetchInterval: 30_000,
  });

  const filtered = React.useMemo(() => {
    const query = search.trim().toLowerCase();
    return (activityQuery.data ?? []).filter((entry) => {
      if (successFilter === "success" && !entry.success) return false;
      if (successFilter === "failure" && entry.success) return false;
      if (!query) return true;
      const haystack = [entry.ipAddress, entry.countryCode, entry.region, entry.city, entry.tenantName, entry.eventType, entry.userAgent]
        .filter(Boolean)
        .join(" ")
        .toLowerCase();
      return haystack.includes(query);
    });
  }, [activityQuery.data, search, successFilter]);

  const failureCount = React.useMemo(() => (activityQuery.data ?? []).filter((e) => !e.success).length, [activityQuery.data]);

  if (!session) {
    return null;
  }

  return (
    <div className="mx-auto w-full max-w-6xl">
      <AdminNav />

      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-lg font-semibold">Atividade de login e cadastro</h1>
          <p className="text-muted-foreground text-sm">
            Ultimos 14 dias, todos os estabelecimentos e o proprio Super Admin -- atualiza a cada 30s.
            {failureCount > 0 && (
              <span className="text-destructive ml-1 font-medium">{failureCount} falha(s) no periodo.</span>
            )}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Input
            placeholder="Buscar por IP, estabelecimento, evento..."
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            className="w-64"
          />
          <div role="group" aria-label="Filtrar por resultado" className="flex gap-1">
            <Button
              type="button"
              size="sm"
              variant={successFilter === "all" ? "default" : "ghost"}
              aria-pressed={successFilter === "all"}
              onClick={() => setSuccessFilter("all")}
            >
              Todos
            </Button>
            <Button
              type="button"
              size="sm"
              variant={successFilter === "failure" ? "default" : "ghost"}
              aria-pressed={successFilter === "failure"}
              onClick={() => setSuccessFilter("failure")}
            >
              Falhas
            </Button>
            <Button
              type="button"
              size="sm"
              variant={successFilter === "success" ? "default" : "ghost"}
              aria-pressed={successFilter === "success"}
              onClick={() => setSuccessFilter("success")}
            >
              Sucessos
            </Button>
          </div>
        </div>
      </div>

      {activityQuery.isLoading ? (
        <div className="flex flex-col gap-2">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
        </div>
      ) : filtered.length === 0 ? (
        <EmptyState icon={ShieldAlert} title="Nenhum evento encontrado" />
      ) : (
        <div className="overflow-x-auto rounded-xl border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Quando</TableHead>
                <TableHead>Evento</TableHead>
                <TableHead>Origem</TableHead>
                <TableHead>Estabelecimento</TableHead>
                <TableHead>IP</TableHead>
                <TableHead>Localização</TableHead>
                <TableHead>Navegador</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.map((entry: SecurityActivityEntry) => (
                <TableRow key={entry.id}>
                  <TableCell className="text-muted-foreground text-xs whitespace-nowrap">
                    {formatDateTime(entry.occurredAtUtc)}
                  </TableCell>
                  <TableCell>
                    <Badge variant={entry.success ? "success" : "destructive"}>
                      {EVENT_LABELS[entry.eventType] ?? entry.eventType}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-muted-foreground text-xs">
                    {entry.source === "Platform" ? "Super Admin" : "Estabelecimento"}
                  </TableCell>
                  <TableCell className="text-sm">{entry.tenantName ?? "—"}</TableCell>
                  <TableCell className="max-w-40 truncate font-mono text-xs" title={entry.ipAddress ?? undefined}>
                    {entry.ipAddress ?? "—"}
                  </TableCell>
                  <TableCell className="text-sm whitespace-nowrap">{formatLocation(entry)}</TableCell>
                  <TableCell className="text-muted-foreground text-xs whitespace-nowrap" title={parseUserAgent(entry.userAgent).full || undefined}>
                    {parseUserAgent(entry.userAgent).label}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  );
}
