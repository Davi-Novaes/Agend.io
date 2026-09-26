"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Check, MailCheck } from "lucide-react";

import {
  listBusinessTypes,
  createTenant,
  registerUser,
  resendConfirmationEmail,
  listPlans,
  onboardSelectPlan,
  type PlanSummary,
  ApiError,
} from "@/lib/api/client";
import { Logo } from "@/components/logo";
import { CursorSpotlight } from "@/components/cursor-spotlight";
import { TurnstileWidget } from "@/components/auth/turnstile-widget";
import { strongPasswordSchema } from "@/lib/validation/password";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Checkbox } from "@/components/ui/checkbox";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { cn } from "@/lib/utils";
import { formatCpfCnpj, formatPhone, isValidCpfCnpj } from "@/lib/format/br-masks";

function slugify(value: string): string {
  return value
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/-{2,}/g, "-")
    .replace(/^-|-$/g, "");
}

const onboardingSchema = z.object({
  businessName: z.string().trim().min(1, "Informe o nome do estabelecimento."),
  slug: z
    .string()
    .trim()
    .min(1, "Informe um identificador.")
    .max(63, "No maximo 63 caracteres."),
  timeZoneId: z.string().trim().min(1, "Selecione um fuso horario."),
  businessType: z.string().min(1, "Selecione um segmento."),
  ownerFullName: z.string().trim().min(1, "Informe seu nome."),
  ownerEmail: z.email("Informe um e-mail valido."),
  ownerPassword: strongPasswordSchema,
  // .min() aqui teria que ser sobre DIGITOS, nao sobre o tamanho da string --
  // com a mascara (ver formatPhone), "(11) 3456-7" ja tem 11 caracteres com
  // so 7 digitos reais, e passaria batido num min(10) ingenuo.
  ownerPhone: z
    .string()
    .trim()
    .refine((value) => value.replace(/\D/g, "").length >= 10, "Informe um telefone valido com DDD."),
  ownerCpfCnpj: z.string().trim().refine(isValidCpfCnpj, "CPF ou CNPJ invalido."),
  termsAccepted: z.boolean().refine((value) => value === true, {
    message: "E preciso aceitar os Termos de Uso e a Politica de Privacidade.",
  }),
});

type OnboardingFormValues = z.infer<typeof onboardingSchema>;

const STEPS = [
  { fields: ["businessName", "slug", "timeZoneId"], title: "Seu estabelecimento" },
  { fields: ["businessType"], title: "Qual e o seu segmento?" },
  {
    fields: ["ownerFullName", "ownerEmail", "ownerPassword", "ownerPhone", "ownerCpfCnpj", "termsAccepted"],
    title: "Sua conta",
  },
] as const satisfies ReadonlyArray<{ fields: ReadonlyArray<keyof OnboardingFormValues>; title: string }>;

const COMMON_TIME_ZONES = [
  "America/Sao_Paulo",
  "America/Manaus",
  "America/Rio_Branco",
  "America/Fortaleza",
  "America/Noronha",
];

/** Camadas de luz ambiente (mesma receita da home) + o spot que segue o
 * cursor -- compartilhado pelas 3 telas do fluxo (formulario, escolha de
 * plano, confirmacao de e-mail), entao fica isolado num componente em vez de
 * repetir o JSX em cada `return`. */
function OnboardingBackground() {
  return (
    <div aria-hidden className="pointer-events-none absolute inset-0 overflow-hidden">
      <div className="hero-glow hero-glow-1" />
      <div className="hero-glow hero-glow-2" />
      <div className="hero-glow hero-glow-3" />
      <CursorSpotlight />
    </div>
  );
}

export default function OnboardingPage() {
  const router = useRouter();
  const [step, setStep] = React.useState(0);
  const [slugTouched, setSlugTouched] = React.useState(false);
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [turnstileToken, setTurnstileToken] = React.useState<string | null>(null);
  const [turnstileNonce, setTurnstileNonce] = React.useState(0);
  const [isResending, setIsResending] = React.useState(false);
  // Presente so apos o cadastro concluir — troca o formulario pela tela de
  // "confirme seu e-mail" em vez de logar automaticamente (login agora exige
  // e-mail confirmado). So chega la depois do passo de plano (abaixo).
  const [registered, setRegistered] = React.useState<{ tenantId: string; email: string; requiresPayment: boolean } | null>(
    null
  );
  // Conta criada, mas plano ainda nao escolhido — controla a tela "Escolha
  // seu plano" (passo final, depois de conta, ver Fase 24). onboardingToken
  // prova posse do tenant nas chamadas de billing abaixo (BL-01).
  const [accountCreated, setAccountCreated] = React.useState<{ tenantId: string; email: string; onboardingToken: string } | null>(
    null
  );
  const [isSelectingPlan, setIsSelectingPlan] = React.useState(false);

  const businessTypesQuery = useQuery({
    queryKey: ["business-types"],
    queryFn: listBusinessTypes,
  });

  const plansQuery = useQuery({
    queryKey: ["billing-plans"],
    queryFn: () => listPlans(),
    enabled: accountCreated !== null,
  });

  const detectedTimeZone = React.useMemo(() => {
    try {
      return Intl.DateTimeFormat().resolvedOptions().timeZone;
    } catch {
      return "America/Sao_Paulo";
    }
  }, []);

  const form = useForm<OnboardingFormValues>({
    resolver: zodResolver(onboardingSchema),
    defaultValues: {
      businessName: "",
      slug: "",
      timeZoneId: detectedTimeZone,
      businessType: "",
      ownerFullName: "",
      ownerEmail: "",
      ownerPassword: "",
      ownerPhone: "",
      ownerCpfCnpj: "",
      termsAccepted: false,
    },
  });

  const businessName = form.watch("businessName");
  const selectedBusinessType = form.watch("businessType");

  React.useEffect(() => {
    if (!slugTouched) {
      form.setValue("slug", slugify(businessName), { shouldValidate: false });
    }
  }, [businessName, slugTouched, form]);

  const selectedDefinition = businessTypesQuery.data?.find((o) => o.value === selectedBusinessType);

  async function goNext() {
    const valid = await form.trigger(STEPS[step].fields);
    if (valid) {
      setStep((s) => Math.min(s + 1, STEPS.length - 1));
    }
  }

  function goBack() {
    // No primeiro passo nao ha pra onde voltar dentro do fluxo -- manda pra
    // home em vez de deixar o botao sem efeito nenhum.
    if (step === 0) {
      router.push("/");
      return;
    }
    setStep((s) => Math.max(s - 1, 0));
  }

  async function onSubmit(values: OnboardingFormValues) {
    if (!turnstileToken) {
      return;
    }

    setIsSubmitting(true);
    try {
      const tenant = await createTenant({
        name: values.businessName,
        slug: values.slug,
        businessType: values.businessType,
        timeZoneId: values.timeZoneId,
      });

      const registered = await registerUser({
        tenantId: tenant.id,
        email: values.ownerEmail,
        password: values.ownerPassword,
        fullName: values.ownerFullName,
        phone: values.ownerPhone,
        cpfCnpj: values.ownerCpfCnpj,
        termsAccepted: values.termsAccepted,
        turnstileToken,
      });

      setAccountCreated({ tenantId: tenant.id, email: values.ownerEmail, onboardingToken: registered.onboardingToken });
    } catch (error) {
      setTurnstileToken(null);
      setTurnstileNonce((n) => n + 1);

      if (error instanceof ApiError && error.status === 409) {
        form.setError("slug", { message: "Este identificador ja esta em uso." });
        setStep(0);
        return;
      }

      const message =
        error instanceof ApiError ? error.message : "Nao foi possivel concluir o cadastro. Tente novamente.";
      toast.error(message);
    } finally {
      setIsSubmitting(false);
    }
  }

  async function onSelectPlan(plan: PlanSummary) {
    if (!accountCreated) {
      return;
    }
    setIsSelectingPlan(true);
    try {
      // So registra a intencao (P1-5, docs/AUTH_BILLING_SECURITY_AUDIT.md) —
      // a ativacao de verdade (Free direto, ou pagamento) so acontece depois
      // de confirmar o e-mail e logar, em /settings/account/plan. Sem checkout
      // nenhum aqui ainda, pago ou gratis.
      const result = await onboardSelectPlan({ planId: plan.id }, accountCreated.onboardingToken);
      setRegistered({ tenantId: accountCreated.tenantId, email: accountCreated.email, requiresPayment: result.requiresPayment });
    } catch (error) {
      const message = error instanceof ApiError ? error.message : "Nao foi possivel selecionar o plano. Tente novamente.";
      toast.error(message);
    } finally {
      setIsSelectingPlan(false);
    }
  }

  async function onResendConfirmation() {
    if (!registered) {
      return;
    }
    setIsResending(true);
    try {
      await resendConfirmationEmail({ tenantId: registered.tenantId, email: registered.email });
      toast.success("E-mail reenviado. Confira sua caixa de entrada.");
    } catch (error) {
      const message = error instanceof ApiError ? error.message : "Nao foi possivel reenviar o e-mail. Tente novamente.";
      toast.error(message);
    } finally {
      setIsResending(false);
    }
  }

  const isLastStep = step === STEPS.length - 1;

  // Checado ANTES de pendingCheckout/accountCreated de proposito: os dois
  // continuam com valor (nunca zerados) depois que o cadastro conclui, entao
  // registered precisa vencer no render ou a tela travaria em "Escolha seu
  // plano"/"Aguardando confirmação" mesmo depois de pronto.
  if (registered) {
    return (
      <main className="relative flex min-h-full flex-1 items-center justify-center overflow-hidden p-4 sm:p-6">
        <OnboardingBackground />
        <div className="relative z-10 w-full max-w-md text-center">
          <div className="mb-6 flex flex-col items-center gap-4">
            <Logo />
            <span className="bg-accent text-accent-foreground flex size-14 items-center justify-center rounded-full">
              <MailCheck className="size-7" aria-hidden />
            </span>
          </div>
          <h1 className="text-xl font-semibold tracking-tight">Confirme seu e-mail</h1>
          <p className="text-muted-foreground mt-2 text-sm text-balance">
            Enviamos um link de confirmação para <strong>{registered.email}</strong>. Clique nele, depois entre com
            sua senha para {registered.requiresPayment ? "concluir o pagamento do seu plano" : "ativar seu plano gratuito"}{" "}
            e acessar o painel.
          </p>
          <Button
            type="button"
            variant="outline"
            className="mt-6"
            onClick={onResendConfirmation}
            disabled={isResending}
          >
            {isResending ? "Reenviando..." : "Reenviar e-mail"}
          </Button>
          <p className="text-muted-foreground mt-6 text-center text-sm">
            Ja confirmou?{" "}
            <Link href="/login" className="text-primary underline-offset-4 hover:underline">
              Entrar
            </Link>
          </p>
        </div>
      </main>
    );
  }

  if (accountCreated) {
    return (
      <main className="relative flex min-h-full flex-1 items-center justify-center overflow-hidden p-4 sm:p-6">
        <OnboardingBackground />
        <div className="relative z-10 w-full max-w-5xl">
          <div className="mb-8 flex flex-col items-center gap-3 text-center">
            <Logo />
            <h1 className="text-xl font-semibold tracking-tight">Escolha seu plano</h1>
            <p className="text-muted-foreground text-sm">Você pode trocar de plano quando quiser depois.</p>
          </div>

          {plansQuery.isLoading && (
            <p className="text-muted-foreground text-center text-sm">Carregando planos...</p>
          )}
          {plansQuery.isError && (
            <p className="text-destructive text-center text-sm">
              Não foi possível carregar os planos. Recarregue a página.
            </p>
          )}

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {plansQuery.data?.map((plan) => {
              const isFree = plan.priceAmount === 0;
              return (
                <button
                  key={plan.id}
                  type="button"
                  onClick={() => onSelectPlan(plan)}
                  disabled={isSelectingPlan}
                  className={cn(
                    "relative flex flex-col items-start gap-2 rounded-xl border p-5 text-left transition-colors disabled:pointer-events-none disabled:opacity-60",
                    plan.isFeatured ? "border-primary bg-accent/40" : "border-border hover:border-primary hover:bg-accent"
                  )}
                >
                  {plan.isFeatured && (
                    <span className="bg-primary text-primary-foreground absolute -top-2.5 right-4 rounded-full px-2 py-0.5 text-[11px] font-medium">
                      Mais popular
                    </span>
                  )}
                  <span className="text-lg font-semibold">{plan.name}</span>
                  <span className="text-2xl font-bold">
                    {isFree
                      ? "Grátis"
                      : `R$ ${plan.priceAmount.toFixed(2).replace(".", ",")}/mês`}
                  </span>
                  <span className="text-muted-foreground text-sm">
                    {isFree
                      ? "Sem cartão, sem compromisso — comece a usar agora."
                      : "14 dias grátis, depois cobrado automaticamente. Cancele quando quiser."}
                  </span>
                  <ul className="text-muted-foreground mt-1 flex flex-col gap-1 text-xs">
                    <li>
                      {plan.maxUnits === null
                        ? "Unidades ilimitadas"
                        : `Até ${plan.maxUnits} unidade${plan.maxUnits === 1 ? "" : "s"}`}
                    </li>
                    <li>
                      {plan.maxProfessionals === null
                        ? "Profissionais ilimitados"
                        : `Até ${plan.maxProfessionals} profissionais`}
                    </li>
                    <li>
                      {plan.maxCustomers === null
                        ? "Clientes ilimitados"
                        : `Até ${plan.maxCustomers.toLocaleString("pt-BR")} clientes`}
                    </li>
                  </ul>
                </button>
              );
            })}
          </div>

          {isSelectingPlan && (
            <p className="text-muted-foreground mt-4 text-center text-sm">Preparando...</p>
          )}
        </div>
      </main>
    );
  }

  return (
    <main className="relative flex min-h-full flex-1 items-center justify-center overflow-hidden p-4 sm:p-6">
      <OnboardingBackground />
      <div className="relative z-10 w-full max-w-lg">
        <div className="mb-8 flex flex-col items-center gap-6 text-center">
          <Logo />
          <div className="flex w-full items-center gap-2" aria-hidden>
            {STEPS.map((_, index) => (
              <span
                key={index}
                className={cn(
                  "h-1.5 flex-1 rounded-full transition-colors",
                  index <= step ? "bg-primary" : "bg-muted"
                )}
              />
            ))}
          </div>
          <p className="text-muted-foreground text-sm">
            Passo {step + 1} de {STEPS.length} — {STEPS[step].title}
          </p>
        </div>

        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="grid gap-5">
            {step === 0 && (
              <>
                <FormField
                  control={form.control}
                  name="businessName"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Nome do estabelecimento</FormLabel>
                      <FormControl>
                        <Input placeholder="Barbearia do Ze" autoFocus {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="slug"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Identificador (link do seu estabelecimento)</FormLabel>
                      <FormControl>
                        <Input
                          {...field}
                          onChange={(event) => {
                            setSlugTouched(true);
                            field.onChange(event);
                          }}
                        />
                      </FormControl>
                      <p className="text-muted-foreground text-xs">
                        agendio.com.br/{field.value || "seu-identificador"}
                      </p>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="timeZoneId"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Fuso horario</FormLabel>
                      <Select value={field.value} onValueChange={field.onChange}>
                        <FormControl>
                          <SelectTrigger className="w-full">
                            <SelectValue />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          {[...new Set([field.value, ...COMMON_TIME_ZONES])].map((tz) => (
                            <SelectItem key={tz} value={tz}>
                              {tz}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </>
            )}

            {step === 1 && (
              <FormField
                control={form.control}
                name="businessType"
                render={({ field }) => (
                  <FormItem>
                    <fieldset>
                      <legend className="sr-only">Segmento do estabelecimento</legend>
                      {businessTypesQuery.isLoading && (
                        <p className="text-muted-foreground text-sm">Carregando segmentos...</p>
                      )}
                      {businessTypesQuery.isError && (
                        <p className="text-destructive text-sm">
                          Nao foi possivel carregar os segmentos. Recarregue a pagina.
                        </p>
                      )}
                      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
                        {businessTypesQuery.data?.map((option) => {
                          const checked = field.value === option.value;
                          return (
                            <label
                              key={option.value}
                              className={cn(
                                "relative flex cursor-pointer items-center justify-center rounded-lg border p-3 text-center text-sm transition-colors",
                                checked
                                  ? "border-primary bg-accent text-accent-foreground font-medium"
                                  : "border-border hover:bg-muted"
                              )}
                            >
                              <input
                                type="radio"
                                value={option.value}
                                checked={checked}
                                onChange={field.onChange}
                                onBlur={field.onBlur}
                                name={field.name}
                                className="sr-only"
                              />
                              {checked && (
                                <Check className="absolute top-1.5 right-1.5 size-3.5" strokeWidth={2.5} />
                              )}
                              {option.displayName}
                            </label>
                          );
                        })}
                      </div>
                    </fieldset>
                    <FormMessage />

                    {selectedDefinition && (
                      <div className="bg-muted mt-2 rounded-lg p-3 text-sm">
                        <p className="text-muted-foreground mb-1.5 text-xs font-medium tracking-wide uppercase">
                          Assim vai aparecer no seu painel
                        </p>
                        <p>
                          <span className="text-muted-foreground">Cliente</span> vira{" "}
                          <strong>{selectedDefinition.terminology.customerPlural}</strong> ·{" "}
                          <span className="text-muted-foreground">Serviço</span> vira{" "}
                          <strong>{selectedDefinition.terminology.servicePlural}</strong> ·{" "}
                          <span className="text-muted-foreground">Profissional</span> vira{" "}
                          <strong>{selectedDefinition.terminology.staffPlural}</strong>
                        </p>
                      </div>
                    )}
                  </FormItem>
                )}
              />
            )}

            {step === 2 && (
              <>
                <FormField
                  control={form.control}
                  name="ownerFullName"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Seu nome</FormLabel>
                      <FormControl>
                        <Input autoComplete="name" autoFocus {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="ownerEmail"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>E-mail</FormLabel>
                      <FormControl>
                        <Input type="email" autoComplete="email" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="ownerPassword"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Senha</FormLabel>
                      <FormControl>
                        <Input type="password" autoComplete="new-password" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="ownerPhone"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Telefone (WhatsApp)</FormLabel>
                      <FormControl>
                        <Input
                          type="tel"
                          autoComplete="tel"
                          placeholder="(11) 99999-9999"
                          {...field}
                          onChange={(event) => field.onChange(formatPhone(event.target.value))}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="ownerCpfCnpj"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>CPF ou CNPJ</FormLabel>
                      <FormControl>
                        <Input
                          inputMode="numeric"
                          autoComplete="off"
                          placeholder="000.000.000-00"
                          {...field}
                          onChange={(event) => field.onChange(formatCpfCnpj(event.target.value))}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="termsAccepted"
                  render={({ field }) => (
                    <FormItem className="flex flex-row items-start gap-2 space-y-0">
                      <FormControl>
                        <Checkbox checked={field.value} onCheckedChange={field.onChange} className="mt-0.5" />
                      </FormControl>
                      <div>
                        <FormLabel className="text-muted-foreground font-normal">
                          Li e aceito os{" "}
                          <Link href="/termos" target="_blank" className="text-primary underline-offset-4 hover:underline">
                            Termos de Uso
                          </Link>{" "}
                          e a{" "}
                          <Link
                            href="/privacidade"
                            target="_blank"
                            className="text-primary underline-offset-4 hover:underline"
                          >
                            Política de Privacidade
                          </Link>
                          .
                        </FormLabel>
                        <FormMessage />
                      </div>
                    </FormItem>
                  )}
                />
                <TurnstileWidget key={turnstileNonce} onVerify={setTurnstileToken} onExpire={() => setTurnstileToken(null)} />
              </>
            )}

            <div className="mt-2 flex items-center justify-between gap-3">
              <Button type="button" variant="ghost" onClick={goBack} disabled={isSubmitting}>
                Voltar
              </Button>
              {isLastStep ? (
                <Button key="submit" type="submit" disabled={isSubmitting || !turnstileToken}>
                  {isSubmitting ? "Criando..." : "Criar estabelecimento"}
                </Button>
              ) : (
                <Button key="next" type="button" onClick={goNext}>
                  Continuar
                </Button>
              )}
            </div>
          </form>
        </Form>

        <p className="text-muted-foreground mt-6 text-center text-sm">
          Ja tem uma conta?{" "}
          <Link href="/login" className="text-primary underline-offset-4 hover:underline">
            Entrar
          </Link>
        </p>
      </div>
    </main>
  );
}
