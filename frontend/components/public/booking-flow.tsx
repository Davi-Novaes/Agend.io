"use client";

import * as React from "react";
import Image from "next/image";
import { useQuery, useMutation } from "@tanstack/react-query";
import { ArrowLeft, ArrowRight, Check, Clock3, Scissors, UserRound } from "lucide-react";
import {
  publicListServices,
  publicListResources,
  getAvailableSlots,
  publicScheduleAppointment,
  joinWaitlist,
  getCustomerPortal,
  ApiError,
  type PublicServiceSummary,
  type PublicResourceSummary,
  type AvailableSlot,
  resolveAssetUrl,
} from "@/lib/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { cn } from "@/lib/utils";
import { formatPhoneDisplay } from "@/lib/format/br-masks";

type Step = "service" | "resource" | "datetime" | "details" | "confirmed";

const STEP_LABELS: Record<Exclude<Step, "confirmed">, string> = {
  service: "Serviço",
  resource: "Profissional",
  datetime: "Data e horário",
  details: "Seus dados",
};

function toDateInputValue(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

function formatPrice(amount: number, currency: string): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency }).format(amount);
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString("pt-BR", { weekday: "long", day: "2-digit", month: "long" });
}

export function BookingFlow({
  tenantId,
  buttonRadiusClassName,
  paymentRequired = false,
  depositPercentage = 0,
  initialServices = [],
  initialResources = [],
  initialServiceId,
  customerPortalHref,
}: {
  tenantId: string;
  /** Estilo de botao do estabelecimento (Fase 3 — Personalizacao da pagina). */
  buttonRadiusClassName?: string;
  /** Fase 16 — se o tenant exige sinal, o formulario passa a pedir CPF e a confirmacao mostra o link de pagamento. */
  paymentRequired?: boolean;
  depositPercentage?: number;
  /** Dados ja carregados no SSR evitam o estado vazio ao abrir a pagina publica. */
  initialServices?: PublicServiceSummary[];
  initialResources?: PublicResourceSummary[];
  /** Permite que um card da vitrine abra o agendamento com o servico escolhido. */
  initialServiceId?: string;
  customerPortalHref?: string;
}) {
  const initialService = initialServices.find((service) => service.id === initialServiceId) ?? null;
  const hasSingleInitialResource = initialResources.length === 1;
  const [step, setStep] = React.useState<Step>(() =>
    initialService ? (hasSingleInitialResource ? "datetime" : "resource") : "service"
  );
  const [selectedService, setSelectedService] = React.useState<PublicServiceSummary | null>(initialService);
  const [selectedResource, setSelectedResource] = React.useState<PublicResourceSummary | null>(
    hasSingleInitialResource ? initialResources[0] : null
  );
  const [selectedDate, setSelectedDate] = React.useState(() => toDateInputValue(new Date()));
  const [selectedSlot, setSelectedSlot] = React.useState<AvailableSlot | null>(null);

  const [fullName, setFullName] = React.useState("");
  const [email, setEmail] = React.useState("");
  const [phone, setPhone] = React.useState("");
  const [cpf, setCpf] = React.useState("");
  const [notes, setNotes] = React.useState("");
  const [formError, setFormError] = React.useState<string | null>(null);
  const [showWaitlistForm, setShowWaitlistForm] = React.useState(false);
  const [paymentUrl, setPaymentUrl] = React.useState<string | null>(null);

  const servicesQuery = useQuery({
    queryKey: ["public-services", tenantId],
    queryFn: () => publicListServices(tenantId),
    initialData: initialServices.length > 0 ? initialServices : undefined,
    staleTime: initialServices.length > 0 ? 60_000 : 0,
  });

  const resourcesQuery = useQuery({
    queryKey: ["public-resources", tenantId],
    queryFn: () => publicListResources(tenantId),
    initialData: initialResources.length > 0 ? initialResources : undefined,
    staleTime: initialResources.length > 0 ? 60_000 : 0,
  });

  const slotsQuery = useQuery({
    queryKey: ["public-availability", tenantId, selectedResource?.id, selectedService?.id, selectedDate],
    queryFn: () =>
      getAvailableSlots(tenantId, { resourceId: selectedResource!.id, serviceId: selectedService!.id, date: selectedDate }),
    enabled: step === "datetime" && Boolean(selectedResource) && Boolean(selectedService),
  });

  // Mesma queryKey do PortalAccountMenu/CustomerPortal — reaproveita o cache
  // em vez de checar a sessao de novo, e responde 401 silenciosamente se o
  // cliente nao estiver logado (ver GetCustomerPortalQueryHandler).
  const customerPortalQuery = useQuery({
    queryKey: ["customer-portal", tenantId],
    queryFn: () => getCustomerPortal(tenantId),
    retry: false,
  });

  // So preenche uma vez, e so o que ainda estiver vazio — nunca sobrescreve o
  // que o cliente ja digitou (ex.: quer agendar pra outra pessoa).
  const hasPrefilledFromPortalRef = React.useRef(false);
  React.useEffect(() => {
    if (!customerPortalQuery.data || hasPrefilledFromPortalRef.current) return;
    hasPrefilledFromPortalRef.current = true;
    const profile = customerPortalQuery.data;
    setFullName((current) => current || profile.fullName);
    setEmail((current) => current || profile.email);
    setPhone((current) => current || (profile.phone ? formatPhoneDisplay(profile.phone) : ""));
  }, [customerPortalQuery.data]);

  const scheduleMutation = useMutation({
    mutationFn: () =>
      publicScheduleAppointment(tenantId, {
        resourceId: selectedResource!.id,
        serviceId: selectedService!.id,
        startAtUtc: selectedSlot!.startUtc,
        customerFullName: fullName.trim(),
        customerEmail: email.trim(),
        customerPhone: phone.trim() === "" ? null : phone.trim(),
        notes: notes.trim() === "" ? null : notes.trim(),
        customerCpf: paymentRequired ? cpf.trim() : null,
      }),
    onSuccess: (result) => {
      setPaymentUrl(result.paymentUrl);
      setStep("confirmed");
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : "Não foi possível concluir o agendamento. Tente novamente.");
    },
  });

  const waitlistMutation = useMutation({
    mutationFn: () =>
      joinWaitlist(tenantId, {
        serviceId: selectedService!.id,
        resourceId: selectedResource?.id ?? null,
        preferredDate: selectedDate,
        customerFullName: fullName.trim(),
        customerEmail: email.trim(),
        customerPhone: phone.trim() === "" ? null : phone.trim(),
        notes: notes.trim() === "" ? null : notes.trim(),
      }),
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : "Não foi possível entrar na lista de espera. Tente novamente.");
    },
  });

  function selectService(service: PublicServiceSummary) {
    setSelectedService(service);
    setSelectedSlot(null);
    const activeResources = resourcesQuery.data ?? [];
    if (activeResources.length <= 1) {
      setSelectedResource(activeResources[0] ?? null);
      setStep("datetime");
    } else {
      setStep("resource");
    }
  }

  function selectResource(resource: PublicResourceSummary) {
    setSelectedResource(resource);
    setSelectedSlot(null);
    setStep("datetime");
  }

  function selectSlot(slot: AvailableSlot) {
    setSelectedSlot(slot);
    setFormError(null);
    setStep("details");
  }

  function handleSubmitDetails(event: React.FormEvent) {
    event.preventDefault();
    setFormError(null);

    if (!fullName.trim() || !email.trim()) {
      setFormError("Preencha nome e e-mail para confirmar.");
      return;
    }

    if (paymentRequired && !cpf.trim()) {
      setFormError("Este estabelecimento exige sinal para confirmar o agendamento — informe seu CPF.");
      return;
    }

    scheduleMutation.mutate();
  }

  const showsResourcePicker = (resourcesQuery.data?.length ?? 0) > 1;
  const stepOrder: Exclude<Step, "confirmed">[] = showsResourcePicker
    ? ["service", "resource", "datetime", "details"]
    : ["service", "datetime", "details"];
  const currentStepIndex = step === "confirmed" ? stepOrder.length : stepOrder.indexOf(step);

  return (
    <div className="mx-auto w-full max-w-3xl">
      {step !== "confirmed" && (
        <div className="mb-8" aria-label="Progresso do agendamento">
          <div className="mb-3 flex items-center justify-between gap-2">
            {stepOrder.map((item, index) => {
              const isComplete = index < currentStepIndex;
              const isCurrent = index === currentStepIndex;
              return (
                <React.Fragment key={item}>
                  {index > 0 && <span aria-hidden className={cn("h-px flex-1", isComplete || isCurrent ? "bg-primary" : "bg-border")} />}
                  <div className="flex min-w-0 flex-col items-center gap-1.5">
                    <span
                      className={cn(
                        "flex size-8 items-center justify-center rounded-full border text-xs font-semibold",
                        isComplete && "border-primary bg-primary text-primary-foreground",
                        isCurrent && "border-primary bg-primary/10 text-primary",
                        !isComplete && !isCurrent && "border-border bg-background text-muted-foreground"
                      )}
                      aria-current={isCurrent ? "step" : undefined}
                    >
                      {isComplete ? <Check className="size-4" /> : index + 1}
                    </span>
                    <span className={cn("hidden text-[11px] font-medium sm:block", isCurrent ? "text-foreground" : "text-muted-foreground")}>
                      {STEP_LABELS[item]}
                    </span>
                  </div>
                </React.Fragment>
              );
            })}
          </div>
          <p className="text-muted-foreground text-center text-xs sm:hidden" aria-live="polite">
            Passo {currentStepIndex + 1} de {stepOrder.length} — {STEP_LABELS[step]}
          </p>
        </div>
      )}

      {step !== "service" && step !== "confirmed" && selectedService && (
        <div className="bg-primary/5 border-primary/15 mb-6 flex flex-wrap items-center gap-x-5 gap-y-2 rounded-xl border px-4 py-3 text-sm">
          <span className="flex items-center gap-2 font-medium"><Scissors className="text-primary size-4" />{selectedService.name}</span>
          <span className="text-muted-foreground flex items-center gap-1.5"><Clock3 className="size-3.5" />{selectedService.durationMinutes} min</span>
          <span className="ml-auto font-semibold">{formatPrice(selectedService.price, selectedService.currency)}</span>
        </div>
      )}

      {step === "service" && (
        <section aria-labelledby="step-service-heading">
          <div className="mb-5">
            <h2 id="step-service-heading" className="text-xl font-semibold tracking-tight">O que você gostaria de agendar?</h2>
            <p className="text-muted-foreground mt-1 text-sm">Escolha uma opção para ver profissionais e horários disponíveis.</p>
          </div>
          {servicesQuery.isLoading && <p className="text-muted-foreground text-sm">Carregando serviços...</p>}
          {servicesQuery.isError && (
            <div className="border-destructive/50 bg-destructive/5 rounded-lg border p-4 text-sm">
              <p className="text-destructive font-medium">Não foi possível carregar os serviços agora.</p>
              <p className="text-muted-foreground mt-1">Tente novamente em instantes — pode ser uma falha temporária, não falta de cadastro.</p>
              <Button type="button" variant="outline" size="sm" className="mt-3" onClick={() => servicesQuery.refetch()}>
                Tentar novamente
              </Button>
            </div>
          )}
          {!servicesQuery.isError && servicesQuery.data?.length === 0 && (
            <p className="text-muted-foreground text-sm">Nenhum serviço disponível no momento.</p>
          )}
          <ul className="grid gap-3 sm:grid-cols-2">
            {(servicesQuery.data ?? []).map((service) => (
              <li key={service.id}>
                <button
                  type="button"
                  onClick={() => selectService(service)}
                  className={cn(
                    "group/service hover:border-primary/60 hover:bg-primary/[0.035] focus-visible:outline-primary flex h-full w-full overflow-hidden rounded-xl border bg-background text-left shadow-xs transition-all hover:-translate-y-0.5 hover:shadow-md focus-visible:outline-2",
                    buttonRadiusClassName
                  )}
                >
                  {service.imageUrl ? (
                    <Image src={resolveAssetUrl(service.imageUrl)} alt="" width={112} height={112} className="h-full min-h-32 w-28 shrink-0 object-cover" unoptimized />
                  ) : (
                    <span className="bg-primary/8 flex min-h-32 w-24 shrink-0 items-center justify-center"><Scissors className="text-primary size-7" strokeWidth={1.5} /></span>
                  )}
                  <span className="flex min-w-0 flex-1 flex-col p-4">
                    {service.category && <span className="text-primary mb-1 text-[11px] font-semibold tracking-wider uppercase">{service.category}</span>}
                    <span className="font-semibold">{service.name}</span>
                    {service.description && <span className="text-muted-foreground mt-1 line-clamp-2 text-xs leading-relaxed">{service.description}</span>}
                    <span className="mt-auto flex items-end justify-between gap-2 pt-4">
                      <span className="text-muted-foreground flex items-center gap-1 text-xs"><Clock3 className="size-3.5" />{service.durationMinutes} min</span>
                      <span className="flex items-center gap-1 font-semibold">{formatPrice(service.price, service.currency)}<ArrowRight className="text-primary size-4 transition-transform group-hover/service:translate-x-0.5" /></span>
                    </span>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </section>
      )}

      {step === "resource" && (
        <section aria-labelledby="step-resource-heading">
          <button type="button" onClick={() => setStep("service")} className="text-muted-foreground hover:text-foreground mb-4 inline-flex items-center gap-1.5 text-sm">
            <ArrowLeft className="size-4" /> Voltar aos serviços
          </button>
          <div className="mb-5">
            <h2 id="step-resource-heading" className="text-xl font-semibold tracking-tight">Com quem você quer ser atendido?</h2>
            <p className="text-muted-foreground mt-1 text-sm">Selecione o profissional de sua preferência.</p>
          </div>
          {resourcesQuery.isLoading && <p className="text-muted-foreground text-sm">Carregando profissionais...</p>}
          {resourcesQuery.isError && (
            <div className="border-destructive/50 bg-destructive/5 rounded-lg border p-4 text-sm">
              <p className="text-destructive font-medium">Não foi possível carregar os profissionais agora.</p>
              <p className="text-muted-foreground mt-1">Tente novamente em instantes.</p>
              <Button type="button" variant="outline" size="sm" className="mt-3" onClick={() => resourcesQuery.refetch()}>
                Tentar novamente
              </Button>
            </div>
          )}
          <ul className="grid gap-3 sm:grid-cols-2">
            {(resourcesQuery.data ?? []).map((resource) => (
              <li key={resource.id}>
                <button
                  type="button"
                  onClick={() => selectResource(resource)}
                  className={cn(
                    "hover:border-primary/60 hover:bg-primary/[0.035] focus-visible:outline-primary flex w-full items-center gap-3 rounded-xl border bg-background p-4 text-left shadow-xs transition-all hover:-translate-y-0.5 hover:shadow-md focus-visible:outline-2",
                    buttonRadiusClassName
                  )}
                >
                  {resource.photoUrl ? (
                    <Image src={resolveAssetUrl(resource.photoUrl)} alt="" width={52} height={52} className="size-13 rounded-full object-cover" unoptimized />
                  ) : (
                    <span className="bg-primary/10 text-primary flex size-13 shrink-0 items-center justify-center rounded-full"><UserRound className="size-5" /></span>
                  )}
                  <span className="min-w-0 flex-1">
                    <span className="block font-semibold">{resource.name}</span>
                    {resource.specialties.length > 0 && <span className="text-muted-foreground mt-0.5 block truncate text-xs">{resource.specialties.join(" · ")}</span>}
                  </span>
                  <ArrowRight className="text-primary size-4" />
                </button>
              </li>
            ))}
          </ul>
        </section>
      )}

      {step === "datetime" && (
        <section aria-labelledby="step-datetime-heading">
          <button
            type="button"
            onClick={() => setStep(showsResourcePicker ? "resource" : "service")}
            className="text-muted-foreground mb-4 text-sm underline"
          >
            Voltar
          </button>
          <h2 id="step-datetime-heading" className="mb-4 text-lg font-medium">
            Escolha data e horário
          </h2>
          <label htmlFor="booking-date" className="mb-1 block text-sm font-medium">
            Data
          </label>
          <Input
            id="booking-date"
            type="date"
            value={selectedDate}
            min={toDateInputValue(new Date())}
            onChange={(event) => {
              setSelectedDate(event.target.value);
              setShowWaitlistForm(false);
              waitlistMutation.reset();
            }}
            className="mb-4 max-w-48"
          />

          {slotsQuery.isLoading && <p className="text-muted-foreground text-sm">Carregando horários...</p>}
          {slotsQuery.isError && (
            <div className="border-destructive/50 bg-destructive/5 rounded-lg border p-4 text-sm">
              <p className="text-destructive font-medium">Não foi possível carregar os horários agora.</p>
              <p className="text-muted-foreground mt-1">Tente novamente em instantes — pode ser uma falha temporária, não falta de horário.</p>
              <Button type="button" variant="outline" size="sm" className="mt-3" onClick={() => slotsQuery.refetch()}>
                Tentar novamente
              </Button>
            </div>
          )}
          {slotsQuery.data && slotsQuery.data.length > 0 && (
            <div className="grid grid-cols-3 gap-2 sm:grid-cols-4">
              {slotsQuery.data.map((slot) => (
                <button
                  key={slot.startUtc}
                  type="button"
                  onClick={() => selectSlot(slot)}
                  className={cn(
                    "hover:border-primary focus-visible:outline-primary rounded-lg border p-2 text-sm focus-visible:outline-2",
                    buttonRadiusClassName
                  )}
                >
                  {formatTime(slot.startUtc)}
                </button>
              ))}
            </div>
          )}

          {slotsQuery.data?.length === 0 && (
            <div>
              <p className="text-muted-foreground text-sm">Nenhum horário disponível nesta data. Tente outra data.</p>

              {waitlistMutation.isSuccess ? (
                <p className="text-sm mt-3">Você entrou na lista de espera! Avisaremos por e-mail se uma vaga abrir.</p>
              ) : showWaitlistForm ? (
                <form
                  className="mt-3 flex flex-col gap-3"
                  onSubmit={(event) => {
                    event.preventDefault();
                    setFormError(null);
                    if (!fullName.trim() || !email.trim()) {
                      setFormError("Preencha nome e e-mail para entrar na lista de espera.");
                      return;
                    }
                    waitlistMutation.mutate();
                  }}
                >
                  <div>
                    <label htmlFor="waitlist-name" className="mb-1 block text-sm font-medium">
                      Nome completo
                    </label>
                    <Input id="waitlist-name" value={fullName} onChange={(event) => setFullName(event.target.value)} required autoComplete="name" />
                  </div>
                  <div>
                    <label htmlFor="waitlist-email" className="mb-1 block text-sm font-medium">
                      E-mail
                    </label>
                    <Input
                      id="waitlist-email"
                      type="email"
                      value={email}
                      onChange={(event) => setEmail(event.target.value)}
                      required
                      autoComplete="email"
                    />
                  </div>
                  <div>
                    <label htmlFor="waitlist-phone" className="mb-1 block text-sm font-medium">
                      Telefone (opcional)
                    </label>
                    <Input id="waitlist-phone" value={phone} onChange={(event) => setPhone(formatPhoneDisplay(event.target.value))} autoComplete="tel" />
                  </div>
                  {formError && (
                    <p role="alert" className="text-destructive text-sm">
                      {formError}
                    </p>
                  )}
                  <Button type="submit" className={buttonRadiusClassName} disabled={waitlistMutation.isPending}>
                    {waitlistMutation.isPending ? "Enviando..." : "Entrar na lista de espera"}
                  </Button>
                </form>
              ) : (
                <Button type="button" variant="outline" className={cn("mt-3", buttonRadiusClassName)} onClick={() => setShowWaitlistForm(true)}>
                  Entrar na lista de espera para este dia
                </Button>
              )}
            </div>
          )}
        </section>
      )}

      {step === "details" && selectedSlot && selectedService && (
        <section aria-labelledby="step-details-heading">
          <button type="button" onClick={() => setStep("datetime")} className="text-muted-foreground mb-4 text-sm underline">
            Voltar
          </button>
          <h2 id="step-details-heading" className="mb-2 text-lg font-medium">
            Seus dados
          </h2>
          <p className="text-muted-foreground mb-4 text-sm capitalize">
            {selectedService.name} · {formatDate(selectedSlot.startUtc)} às {formatTime(selectedSlot.startUtc)}
          </p>
          {customerPortalQuery.data && (
            <p className="border-primary/15 bg-primary/5 text-muted-foreground mb-4 rounded-lg border px-3 py-2 text-sm">
              Preenchido com os dados de <span className="text-foreground font-medium">{customerPortalQuery.data.fullName}</span>, da sua conta.
            </p>
          )}
          <form onSubmit={handleSubmitDetails} className="flex flex-col gap-3">
            <div>
              <label htmlFor="booking-name" className="mb-1 block text-sm font-medium">
                Nome completo
              </label>
              <Input id="booking-name" value={fullName} onChange={(event) => setFullName(event.target.value)} required autoComplete="name" />
            </div>
            <div>
              <label htmlFor="booking-email" className="mb-1 block text-sm font-medium">
                E-mail
              </label>
              <Input
                id="booking-email"
                type="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                required
                autoComplete="email"
              />
            </div>
            <div>
              <label htmlFor="booking-phone" className="mb-1 block text-sm font-medium">
                Telefone (opcional)
              </label>
              <Input id="booking-phone" value={phone} onChange={(event) => setPhone(formatPhoneDisplay(event.target.value))} autoComplete="tel" />
            </div>
            {paymentRequired && (
              <div>
                <label htmlFor="booking-cpf" className="mb-1 block text-sm font-medium">
                  CPF
                </label>
                <Input id="booking-cpf" value={cpf} onChange={(event) => setCpf(event.target.value)} required inputMode="numeric" />
                <p className="text-muted-foreground mt-1 text-xs">
                  Este estabelecimento exige um sinal de {depositPercentage}% do valor do serviço para confirmar o agendamento.
                </p>
              </div>
            )}
            <div>
              <label htmlFor="booking-notes" className="mb-1 block text-sm font-medium">
                Observações (opcional)
              </label>
              <Textarea id="booking-notes" rows={2} value={notes} onChange={(event) => setNotes(event.target.value)} />
            </div>
            {formError && (
              <p role="alert" className="text-destructive text-sm">
                {formError}
              </p>
            )}
            <Button type="submit" className={buttonRadiusClassName} disabled={scheduleMutation.isPending}>
              {scheduleMutation.isPending ? "Confirmando..." : "Confirmar agendamento"}
            </Button>
          </form>
        </section>
      )}

      {step === "confirmed" && selectedService && selectedSlot && (
        <section aria-labelledby="step-confirmed-heading" className="text-center">
          <h2 id="step-confirmed-heading" className="mb-2 text-lg font-medium">
            {paymentUrl
              ? "Falta pouco! Pague o sinal para confirmar"
              : paymentRequired
                ? "Horário reservado — pagamento do sinal pendente"
                : "Agendamento confirmado!"}
          </h2>
          <p className="text-muted-foreground text-sm capitalize">
            {selectedService.name} em {formatDate(selectedSlot.startUtc)} às {formatTime(selectedSlot.startUtc)}
          </p>
          {paymentUrl ? (
            <>
              <p className="text-muted-foreground mt-4 text-sm">
                Seu horário está reservado, mas só é confirmado após o pagamento do sinal.
              </p>
              <Button asChild className={cn("mt-4", buttonRadiusClassName)}>
                <a href={paymentUrl} target="_blank" rel="noopener noreferrer">
                  Pagar sinal agora
                </a>
              </Button>
            </>
          ) : paymentRequired ? (
            // Agendamento foi criado normalmente, mas a geracao da cobranca no
            // gateway falhou (ver TryCreateDepositAsync no backend) — sem este
            // aviso, o cliente via "Agendamento confirmado!" sem nunca saber
            // que ainda precisa pagar o sinal, e a reserva podia expirar sem
            // ele entender o motivo.
            <p className="text-muted-foreground mt-4 text-sm">
              Seu horário está reservado, mas não conseguimos gerar o link de pagamento do sinal agora. A equipe vai
              entrar em contato por e-mail ou telefone para combinar o pagamento antes do horário.
            </p>
          ) : (
            <p className="text-muted-foreground mt-4 text-sm">Enviamos os detalhes para {email}.</p>
          )}
          {customerPortalHref && (
            <Button variant="outline" className={cn("mt-4", buttonRadiusClassName)} asChild>
              <a href={customerPortalHref}>Ver meus agendamentos</a>
            </Button>
          )}
        </section>
      )}
    </div>
  );
}
