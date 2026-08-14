"use client";

import * as React from "react";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { UserRoundX } from "lucide-react";

import {
  getCustomerRecoveryCandidates,
  sendCustomerMessage,
  ApiError,
  type CustomerRecoveryCandidate,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Textarea } from "@/components/ui/textarea";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog";

export function CustomerRecoveryCard() {
  const { session } = useSession();
  const accessToken = session?.accessToken ?? "";
  const queryClient = useQueryClient();
  const [messagingCandidate, setMessagingCandidate] = React.useState<CustomerRecoveryCandidate | null>(null);
  const [subject, setSubject] = React.useState("");
  const [body, setBody] = React.useState("");

  const recoveryQuery = useQuery({
    queryKey: ["customers", "recovery"],
    queryFn: () => getCustomerRecoveryCandidates(accessToken),
    enabled: Boolean(session),
  });

  const sendMutation = useMutation({
    mutationFn: () => {
      if (!messagingCandidate) {
        throw new Error("Nenhum cliente selecionado.");
      }
      return sendCustomerMessage(messagingCandidate.customerId, { subject, body }, accessToken);
    },
    onSuccess: () => {
      toast.success("Mensagem enviada.");
      queryClient.invalidateQueries({ queryKey: ["customers", "recovery"] });
      setMessagingCandidate(null);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel enviar a mensagem."),
  });

  function openMessageDialog(candidate: CustomerRecoveryCandidate) {
    setMessagingCandidate(candidate);
    setSubject("Sentimos sua falta!");
    setBody(`Ola, ${candidate.customerName.split(" ")[0]}! Faz um tempinho que voce nao aparece — que tal marcar um novo horario?`);
  }

  if (recoveryQuery.isLoading) {
    return <Skeleton className="h-32 w-full" />;
  }

  // Sem clientes atrasados: nao ha nada pra decidir aqui, entao o card nem aparece.
  if (!recoveryQuery.data || recoveryQuery.data.length === 0) {
    return null;
  }

  return (
    <>
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <UserRoundX className="size-4" />
            Clientes para recuperar
          </CardTitle>
          <CardDescription>
            Clientes atrasados em relacao ao proprio intervalo habitual de retorno, sem agendamento futuro marcado.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          {recoveryQuery.data.map((candidate) => (
            <div
              key={candidate.customerId}
              className="flex flex-col gap-2 rounded-lg border border-border p-3 sm:flex-row sm:items-center sm:justify-between"
            >
              <p className="text-sm">
                <span className="font-medium">{candidate.customerName}</span> esta ha{" "}
                <span className="font-medium text-warning">{candidate.daysOverdue} dias</span> alem do intervalo habitual
                (costuma voltar a cada ~{candidate.averageIntervalDays} dias).
              </p>
              <div className="flex shrink-0 gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!candidate.customerEmail}
                  title={candidate.customerEmail ? undefined : "Cliente sem e-mail cadastrado"}
                  onClick={() => openMessageDialog(candidate)}
                >
                  Enviar mensagem
                </Button>
                <Button variant="outline" size="sm" asChild>
                  <Link href="/marketing">Criar campanha</Link>
                </Button>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>

      <Dialog open={Boolean(messagingCandidate)} onOpenChange={(open) => !open && setMessagingCandidate(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Enviar mensagem para {messagingCandidate?.customerName}</DialogTitle>
            <DialogDescription>Envio avulso por e-mail — nao cria uma campanha.</DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-3">
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium" htmlFor="recovery-subject">
                Assunto
              </label>
              <Input id="recovery-subject" value={subject} onChange={(event) => setSubject(event.target.value)} />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium" htmlFor="recovery-body">
                Mensagem
              </label>
              <Textarea id="recovery-body" rows={5} value={body} onChange={(event) => setBody(event.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button
              type="button"
              disabled={sendMutation.isPending || !subject.trim() || !body.trim()}
              onClick={() => sendMutation.mutate()}
            >
              {sendMutation.isPending ? "Enviando..." : "Enviar"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
