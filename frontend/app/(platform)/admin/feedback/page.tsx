"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";

import { getPlatformFeedback, type PlatformFeedbackEntry } from "@/lib/api/client";
import { usePlatformSession } from "@/lib/auth/platform-session-context";
import { AdminNav } from "@/components/platform/admin-nav";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { MessageSquare } from "lucide-react";

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString("pt-BR", { dateStyle: "short", timeStyle: "medium" });
}

export default function PlatformFeedbackPage() {
  const router = useRouter();
  const { session } = usePlatformSession();

  const [search, setSearch] = React.useState("");

  React.useEffect(() => {
    if (!session) {
      router.replace("/admin/login");
    }
  }, [session, router]);

  const accessToken = session?.accessToken ?? "";

  const feedbackQuery = useQuery({
    queryKey: ["platform", "feedback"],
    queryFn: () => getPlatformFeedback(accessToken),
    enabled: Boolean(session),
  });

  const filtered = React.useMemo(() => {
    const query = search.trim().toLowerCase();
    if (!query) return feedbackQuery.data ?? [];
    return (feedbackQuery.data ?? []).filter((entry) =>
      [entry.subject, entry.body, entry.tenantName].filter(Boolean).join(" ").toLowerCase().includes(query)
    );
  }, [feedbackQuery.data, search]);

  if (!session) {
    return null;
  }

  return (
    <div className="mx-auto w-full max-w-6xl">
      <AdminNav />

      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-lg font-semibold">Feedback dos estabelecimentos</h1>
          <p className="text-muted-foreground text-sm">Enviado por qualquer usuario autenticado, mais recente primeiro.</p>
        </div>
        <Input
          placeholder="Buscar por assunto, mensagem, estabelecimento..."
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          className="w-72"
        />
      </div>

      {feedbackQuery.isLoading ? (
        <div className="flex flex-col gap-2">
          <Skeleton className="h-24 w-full" />
          <Skeleton className="h-24 w-full" />
          <Skeleton className="h-24 w-full" />
        </div>
      ) : filtered.length === 0 ? (
        <EmptyState icon={MessageSquare} title="Nenhum feedback encontrado" />
      ) : (
        <div className="flex flex-col gap-3">
          {filtered.map((entry: PlatformFeedbackEntry) => (
            <div key={entry.id} className="rounded-xl border p-4">
              <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
                <h2 className="font-medium">{entry.subject}</h2>
                <div className="text-muted-foreground flex items-center gap-2 text-xs whitespace-nowrap">
                  <span>{entry.tenantName ?? "Estabelecimento removido"}</span>
                  <span>·</span>
                  <span>{formatDateTime(entry.createdAtUtc)}</span>
                </div>
              </div>
              <p className="text-sm whitespace-pre-wrap">{entry.body}</p>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
