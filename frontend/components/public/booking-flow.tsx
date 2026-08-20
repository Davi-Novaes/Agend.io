"use client";

import * as React from "react";
import { useQuery, useMutation } from "@tanstack/react-query";
import {
  publicListServices,
  publicListResources,
  getAvailableSlots,
  publicScheduleAppointment,
  joinWaitlist,
  ApiError,
  type PublicServiceSummary,
  type PublicResourceSummary,
  type AvailableSlot,
} from "@/lib/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { cn } from "@/lib/utils";

type Step = "service" | "resource" | "datetime" | "details" | "confirmed";

const STEP_LABELS: Record<Exclude<Step, "confirmed">, string> = {
  service: "Servico",
  resource: "Profissional",
  datetime: "Data e horario",
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
}: {
  tenantId: string;
  /** Estilo de botao do estabelecimento (Fase 3 — Personalizacao da pagina). */
  buttonRadiusClassName?: string;
  /** Fase 16 — se o tenant exige sinal, o formulario passa a pedir CPF e a confirmacao mostra o link de pagamento. */
  paymentRequired?: boolean;
  depositPercentage?: number;
}) {
  const [step, setStep] = React.useState<Step>("service");
  const [selectedService, setSelectedService] = React.useState<PublicServiceSummary | null>(null);
  const [selectedResource, setSelectedResource] = React.useState<PublicResourceSummary | null>(null);
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
  });

  const resourcesQuery = useQuery({
    queryKey: ["public-resources", tenantId],
    queryFn: () => publicListResources(tenantId),
  });

  const slotsQuery = useQuery({
    queryKey: ["public-availability", tenantId, selectedResource?.id, selectedService?.id, selectedDate],
    queryFn: () =>
      getAvailableSlots(tenantId, { resourceId: selectedResource!.id, serviceId: selectedService!.id, date: selectedDate }),
    enabled: step === "datetime" && Boolean(selectedResource) && Boolean(selectedService),
  });

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
      setFormError(error instanceof ApiError ? error.message : "Nao foi possivel concluir o agendamento. Tente novamente.");
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
      setFormError(error instanceof ApiError ? error.message : "Nao foi possivel entrar na lista de espera. Tente novamente.");
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
    <div className="mx-auto w-full max-w-lg">
      {step !== "confirmed" && (
        <p className="text-muted-foreground mb-6 text-center text-xs" aria-live="polite">
          Passo {currentStepIndex + 1} de {stepOrder.length} — {STEP_LABELS[step]}
        </p>
      )}

      {step === "service" && (
        <section aria-labelledby="step-service-heading">
          <h2 id="step-service-heading" className="mb-4 text-lg font-medium">
            Escolha um servico
          </h2>
          {servicesQuery.isLoading && <p className="text-muted-foreground text-sm">Carregando servicos...</p>}
          {servicesQuery.isError && (
            <div className="border-destructive/50 bg-destructive/5 rounded-lg border p-4 text-sm">
              <p className="text-destructive font-medium">Nao foi possivel carregar os servicos agora.</p>
              <p className="text-muted-foreground mt-1">Tente novamente em instantes — pode ser uma falha temporaria, nao falta de cadastro.</p>
              <Button type="button" variant="outline" size="sm" className="mt-3" onClick={() => servicesQuery.refetch()}>
                Tentar novamente
              </Button>
            </div>
          )}
          {!servicesQuery.isError && servicesQuery.data?.length === 0 && (
            <p className="text-muted-foreground text-sm">Nenhum servico disponivel no momento.</p>
          )}
          <ul className="flex flex-col gap-2">
            {(servicesQuery.data ?? []).map((service) => (
              <li key={service.id}>
                <button
                  type="button"
                  onClick={() => selectService(service)}
                  className={cn(
                    "hover:border-primary focus-visible:outline-primary w-full rounded-lg border p-4 text-left focus-visible:outline-2",
                    buttonRadiusClassName
                  )}
                >
                  <p className="font-medium">{service.name}</p>
                  <p className="text-muted-foreground text-sm">
                    {service.durationMinutes} min · {formatPrice(service.price, service.currency)}
                  </p>
                  {service.description && <p className="text-muted-foreground mt-1 text-sm">{service.description}</p>}
                </button>
              </li>
            ))}
          </ul>
        </section>
      )}

      {step === "resource" && (
        <section aria-labelledby="step-resource-heading">
          <button type="button" onClick={() => setStep("service")} className="text-muted-foreground mb-4 text-sm underline">
            Voltar
          </button>
          <h2 id="step-resource-heading" className="mb-4 text-lg font-medium">
            Escolha o profissional
          </h2>
          {resourcesQuery.isLoading && <p className="text-muted-foreground text-sm">Carregando profissionais...</p>}
          {resourcesQuery.isError && (
            <div className="border-destructive/50 bg-destructive/5 rounded-lg border p-4 text-sm">
              <p className="text-destructive font-medium">Nao foi possivel carregar os profissionais agora.</p>
              <p className="text-muted-foreground mt-1">Tente novamente em instantes.</p>
              <Button type="button" variant="outline" size="sm" className="mt-3" onClick={() => resourcesQuery.refetch()}>
                Tentar novamente
              </Button>
            </div>
          )}
          <ul className="flex flex-col gap-2">
            {(resourcesQuery.data ?? []).map((resource) => (
              <li key={resource.id}>
                <button
                  type="button"
                  onClick={() => selectResource(resource)}
                  className={cn(
                    "hover:border-primary focus-visible:outline-primary w-full rounded-lg border p-4 text-left focus-visible:outline-2",
                    buttonRadiusClassName
                  )}
                >
                  <p className="font-medium">{resource.name}</p>
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
            Escolha data e horario
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

          {slotsQuery.isLoading && <p className="text-muted-foreground text-sm">Carregando horarios...</p>}
          {slotsQuery.isError && (
            <div className="border-destructive/50 bg-destructive/5 rounded-lg border p-4 text-sm">
              <p className="text-destructive font-medium">Nao foi possivel carregar os horarios agora.</p>
              <p className="text-muted-foreground mt-1">Tente novamente em instantes — pode ser uma falha temporaria, nao falta de horario.</p>
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
              <p className="text-muted-foreground text-sm">Nenhum horario disponivel nesta data. Tente outra data.</p>

              {waitlistMutation.isSuccess ? (
                <p className="text-sm mt-3">Voce entrou na lista de espera! Avisaremos por e-mail se uma vaga abrir.</p>
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
                    <Input id="waitlist-phone" value={phone} onChange={(event) => setPhone(event.target.value)} autoComplete="tel" />
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
            {selectedService.name} · {formatDate(selectedSlot.startUtc)} as {formatTime(selectedSlot.startUtc)}
          </p>
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
              <Input id="booking-phone" value={phone} onChange={(event) => setPhone(event.target.value)} autoComplete="tel" />
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
                Observacoes (opcional)
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
            {paymentUrl ? "Falta pouco! Pague o sinal para confirmar" : "Agendamento confirmado!"}
          </h2>
          <p className="text-muted-foreground text-sm capitalize">
            {selectedService.name} em {formatDate(selectedSlot.startUtc)} as {formatTime(selectedSlot.startUtc)}
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
          ) : (
            <p className="text-muted-foreground mt-4 text-sm">Enviamos os detalhes para {email}.</p>
          )}
        </section>
      )}
    </div>
  );
}
