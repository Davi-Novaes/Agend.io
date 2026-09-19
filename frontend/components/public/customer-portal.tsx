"use client";

import * as React from "react";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { CalendarDays, CheckCircle2, Clock3, Gift, LogOut, Mail, RefreshCw, Scissors, ShieldCheck, UserPlus, UserRound } from "lucide-react";

import {
  ApiError,
  getCustomerPortal,
  logoutCustomerPortal,
  registerCustomerPortalAccount,
  requestCustomerPortalCode,
  verifyCustomerPortalCode,
  type CustomerPortalAppointment,
} from "@/lib/api/client";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";

const STATUS_LABELS: Record<CustomerPortalAppointment["status"], string> = {
  Scheduled: "Agendado",
  Confirmed: "Confirmado",
  InProgress: "Em andamento",
  Completed: "Concluído",
  CancelledByCustomer: "Cancelado por você",
  CancelledByStaff: "Cancelado pelo estabelecimento",
  NoShow: "Não compareceu",
};

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString("pt-BR", { weekday: "long", day: "2-digit", month: "long", year: "numeric" });
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
}

function formatPrice(amount: number, currency: string): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency }).format(amount);
}

function AppointmentCard({ appointment, repeatHref }: { appointment: CustomerPortalAppointment; repeatHref?: string }) {
  const cancelled = appointment.status === "CancelledByCustomer" || appointment.status === "CancelledByStaff";

  return (
    <Card className={cn("shadow-sm", cancelled && "opacity-65")}>
      <CardContent className="grid gap-4 p-5 sm:grid-cols-[1fr_auto] sm:items-center">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="font-semibold">{appointment.serviceName}</h3>
            <span className={cn("rounded-full px-2 py-0.5 text-[11px] font-semibold", cancelled ? "bg-muted text-muted-foreground" : "bg-primary/10 text-primary")}>
              {STATUS_LABELS[appointment.status]}
            </span>
          </div>
          <p className="text-muted-foreground mt-2 flex items-center gap-1.5 text-sm capitalize"><CalendarDays className="size-4" />{formatDate(appointment.startAtUtc)}</p>
          <div className="text-muted-foreground mt-1 flex flex-wrap gap-x-4 gap-y-1 text-sm">
            <span className="flex items-center gap-1.5"><Clock3 className="size-4" />{formatTime(appointment.startAtUtc)}</span>
            <span className="flex items-center gap-1.5"><UserRound className="size-4" />{appointment.resourceName}</span>
          </div>
        </div>
        <div className="flex items-center gap-3 sm:flex-col sm:items-end"><p className="font-semibold">{formatPrice(appointment.price, appointment.currency)}</p>{repeatHref && <Button variant="outline" size="sm" asChild><Link href={repeatHref}><RefreshCw className="size-3.5" />Agendar novamente</Link></Button>}</div>
      </CardContent>
    </Card>
  );
}

export function CustomerPortal({ tenantId, slug, tenantName }: { tenantId: string; slug: string; tenantName: string }) {
  const queryClient = useQueryClient();
  const [mode, setMode] = React.useState<"login" | "register">("login");
  const [email, setEmail] = React.useState("");
  const [fullName, setFullName] = React.useState("");
  const [phone, setPhone] = React.useState("");
  const [code, setCode] = React.useState("");
  const [codeSent, setCodeSent] = React.useState(false);
  const [openedAt] = React.useState(() => Date.now());

  const profileQuery = useQuery({
    queryKey: ["customer-portal", tenantId],
    queryFn: () => getCustomerPortal(tenantId),
    retry: false,
  });

  const requestCodeMutation = useMutation({
    mutationFn: () => requestCustomerPortalCode(tenantId, email.trim()),
    onSuccess: () => {
      setCode("");
      setCodeSent(true);
    },
  });

  const registerMutation = useMutation({
    mutationFn: () =>
      registerCustomerPortalAccount(tenantId, {
        fullName: fullName.trim(),
        email: email.trim(),
        phone: phone.trim() === "" ? null : phone.trim(),
      }),
    onSuccess: () => {
      setCode("");
      setCodeSent(true);
    },
  });

  const verifyCodeMutation = useMutation({
    mutationFn: () => verifyCustomerPortalCode(tenantId, email.trim(), code),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["customer-portal", tenantId] });
    },
  });

  const logoutMutation = useMutation({
    mutationFn: () => logoutCustomerPortal(tenantId),
    // setQueryData(key, undefined) e um no-op no TanStack Query (updater que
    // resolve pra undefined e ignorado) — removeQueries e o jeito certo de
    // limpar o cache pra a UI voltar pro formulario de login na hora.
    onSuccess: () => {
      queryClient.removeQueries({ queryKey: ["customer-portal", tenantId] });
      setCodeSent(false);
      setCode("");
    },
  });

  if (profileQuery.isLoading) {
    return <div className="text-muted-foreground py-16 text-center text-sm">Verificando sua sessão...</div>;
  }

  if (profileQuery.data) {
    const upcoming = profileQuery.data.appointments
      .filter((appointment) => new Date(appointment.startAtUtc).getTime() >= openedAt && !appointment.status.startsWith("Cancelled"))
      .sort((a, b) => new Date(a.startAtUtc).getTime() - new Date(b.startAtUtc).getTime());
    const history = profileQuery.data.appointments.filter((appointment) => !upcoming.some((item) => item.id === appointment.id));

    return (
      <div className="flex flex-col gap-8">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="text-muted-foreground text-sm">Olá,</p>
            <h1 className="text-2xl font-semibold tracking-tight sm:text-3xl">{profileQuery.data.fullName}</h1>
            <p className="text-muted-foreground mt-1 text-sm">{profileQuery.data.email}{profileQuery.data.phone ? ` · ${profileQuery.data.phone}` : ""}</p>
          </div>
          <Button variant="outline" onClick={() => logoutMutation.mutate()} disabled={logoutMutation.isPending}>
            <LogOut className="size-4" /> Sair
          </Button>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <Card className="border-primary/15 bg-primary/5"><CardContent className="flex items-center gap-4 p-5"><span className="bg-primary text-primary-foreground flex size-11 items-center justify-center rounded-xl"><CalendarDays className="size-5" /></span><div><p className="text-muted-foreground text-xs">Próximos horários</p><p className="text-2xl font-semibold">{upcoming.length}</p></div></CardContent></Card>
          <Card><CardContent className="flex items-center gap-4 p-5"><span className="bg-primary/10 text-primary flex size-11 items-center justify-center rounded-xl"><Gift className="size-5" /></span><div><p className="text-muted-foreground text-xs">Pontos disponíveis</p><p className="text-2xl font-semibold">{profileQuery.data.loyaltyPoints}</p>{profileQuery.data.loyaltyRewardDescription && <p className="text-muted-foreground mt-0.5 text-xs">{profileQuery.data.loyaltyRewardDescription}</p>}</div></CardContent></Card>
        </div>

        <section aria-labelledby="upcoming-heading">
          <div className="mb-4 flex items-center justify-between gap-4"><div><h2 id="upcoming-heading" className="text-xl font-semibold">Próximos agendamentos</h2><p className="text-muted-foreground mt-1 text-sm">Seus horários reservados em {tenantName}.</p></div><Button asChild><Link href={`/${slug}#agendar`}><Scissors className="size-4" />Novo agendamento</Link></Button></div>
          {upcoming.length > 0 ? <div className="grid gap-3">{upcoming.map((appointment) => <AppointmentCard key={appointment.id} appointment={appointment} />)}</div> : <div className="rounded-2xl border border-dashed p-8 text-center"><CalendarDays className="text-muted-foreground mx-auto size-8" /><p className="mt-3 font-medium">Nenhum horário marcado</p><p className="text-muted-foreground mt-1 text-sm">Quando você agendar, o horário aparecerá aqui.</p></div>}
        </section>

        {history.length > 0 && <section aria-labelledby="history-heading"><h2 id="history-heading" className="mb-4 text-xl font-semibold">Histórico</h2><div className="grid gap-3">{history.map((appointment) => <AppointmentCard key={appointment.id} appointment={appointment} repeatHref={`/${slug}?servico=${encodeURIComponent(appointment.serviceId)}#agendar`} />)}</div></section>}
      </div>
    );
  }

  const isUnauthorized = profileQuery.error instanceof ApiError && profileQuery.error.status === 401;
  if (!isUnauthorized && profileQuery.isError) {
    return <div className="rounded-2xl border border-destructive/30 bg-destructive/5 p-6 text-center"><p className="font-medium">Não foi possível abrir sua conta agora.</p><Button variant="outline" className="mt-4" onClick={() => profileQuery.refetch()}><RefreshCw className="size-4" />Tentar novamente</Button></div>;
  }

  const headerIcon = codeSent ? <Mail className="size-5" /> : mode === "register" ? <UserPlus className="size-5" /> : <ShieldCheck className="size-5" />;
  const headerTitle = codeSent ? "Confira seu e-mail" : mode === "register" ? "Criar minha conta" : "Acesse sua conta";
  const headerDescription = codeSent
    ? `Se ${email} estiver cadastrado, o código de 6 dígitos chegará em instantes.`
    : mode === "register"
      ? "Informe seus dados uma vez. Depois disso, basta o e-mail para entrar — sem senha."
      : "Use o e-mail já cadastrado. Se ainda não é cliente, crie sua conta abaixo.";

  return (
    <Card className="mx-auto w-full max-w-lg border-primary/15 shadow-xl">
      <CardContent className="p-6 sm:p-8">
        <div className="mb-7 text-center">
          <span className="bg-primary/10 text-primary mx-auto flex size-12 items-center justify-center rounded-2xl">{headerIcon}</span>
          <h1 className="mt-4 text-2xl font-semibold tracking-tight">{headerTitle}</h1>
          <p className="text-muted-foreground mt-2 text-sm leading-relaxed">{headerDescription}</p>
        </div>

        {!codeSent && mode === "login" && (
          <form className="grid gap-4" onSubmit={(event) => { event.preventDefault(); if (email.trim()) requestCodeMutation.mutate(); }}>
            <div><label htmlFor="portal-email" className="mb-1.5 block text-sm font-medium">E-mail</label><Input id="portal-email" type="email" value={email} onChange={(event) => setEmail(event.target.value)} placeholder="seu-email@exemplo.com" autoComplete="email" required /></div>
            <Button type="submit" size="lg" disabled={requestCodeMutation.isPending}>{requestCodeMutation.isPending ? "Enviando..." : "Receber código de acesso"}<Mail className="size-4" /></Button>
            {requestCodeMutation.isError && <p role="alert" className="text-destructive text-center text-sm">Não foi possível enviar o código. Tente novamente.</p>}
            <Button type="button" variant="ghost" onClick={() => setMode("register")}>Ainda não é cliente? Criar conta</Button>
          </form>
        )}

        {!codeSent && mode === "register" && (
          <form className="grid gap-4" onSubmit={(event) => { event.preventDefault(); if (fullName.trim() && email.trim()) registerMutation.mutate(); }}>
            <div><label htmlFor="portal-register-name" className="mb-1.5 block text-sm font-medium">Nome completo</label><Input id="portal-register-name" value={fullName} onChange={(event) => setFullName(event.target.value)} placeholder="Seu nome" autoComplete="name" required /></div>
            <div><label htmlFor="portal-register-email" className="mb-1.5 block text-sm font-medium">E-mail</label><Input id="portal-register-email" type="email" value={email} onChange={(event) => setEmail(event.target.value)} placeholder="seu-email@exemplo.com" autoComplete="email" required /></div>
            <div><label htmlFor="portal-register-phone" className="mb-1.5 block text-sm font-medium">Telefone (opcional)</label><Input id="portal-register-phone" value={phone} onChange={(event) => setPhone(event.target.value)} autoComplete="tel" /></div>
            <Button type="submit" size="lg" disabled={registerMutation.isPending}>{registerMutation.isPending ? "Criando..." : "Criar conta e receber código"}<UserPlus className="size-4" /></Button>
            {registerMutation.isError && <p role="alert" className="text-destructive text-center text-sm">{registerMutation.error instanceof ApiError ? registerMutation.error.message : "Não foi possível criar sua conta. Tente novamente."}</p>}
            <Button type="button" variant="ghost" onClick={() => setMode("login")}>Já é cliente? Entrar</Button>
          </form>
        )}

        {codeSent && (
          <form className="grid gap-4" onSubmit={(event) => { event.preventDefault(); if (code.length === 6) verifyCodeMutation.mutate(); }}>
            <div><label htmlFor="portal-code" className="mb-1.5 block text-sm font-medium">Código de acesso</label><Input id="portal-code" value={code} onChange={(event) => setCode(event.target.value.replace(/\D/g, "").slice(0, 6))} placeholder="000000" inputMode="numeric" autoComplete="one-time-code" maxLength={6} className="h-12 text-center text-xl tracking-[0.35em]" required /></div>
            <Button type="submit" size="lg" disabled={verifyCodeMutation.isPending || code.length !== 6}>{verifyCodeMutation.isPending ? "Verificando..." : "Entrar na minha conta"}<CheckCircle2 className="size-4" /></Button>
            {verifyCodeMutation.isError && <p role="alert" className="text-destructive text-center text-sm">{verifyCodeMutation.error instanceof ApiError ? verifyCodeMutation.error.message : "Código inválido ou expirado."}</p>}
            <Button type="button" variant="ghost" onClick={() => { setCodeSent(false); setCode(""); verifyCodeMutation.reset(); requestCodeMutation.reset(); registerMutation.reset(); }}>Usar outro e-mail</Button>
          </form>
        )}

        {process.env.NODE_ENV === "development" && (
          <p className="mt-5 rounded-xl border border-amber-500/20 bg-amber-500/10 px-4 py-3 text-center text-xs leading-relaxed text-amber-200">
            Ambiente local: os e-mails são capturados pelo <a className="font-semibold underline underline-offset-2" href="http://localhost:8025" target="_blank" rel="noreferrer">MailHog</a>, não pela sua caixa de entrada.
          </p>
        )}

        <p className="text-muted-foreground mt-6 text-center text-xs">Por segurança, o código expira em 10 minutos e só pode ser usado uma vez.</p>
      </CardContent>
    </Card>
  );
}
