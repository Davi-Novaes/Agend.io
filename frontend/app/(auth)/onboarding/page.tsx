"use client";

import * as React from "react";
import Link from "next/link";
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
  ApiError,
} from "@/lib/api/client";
import { Logo } from "@/components/logo";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { cn } from "@/lib/utils";

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
  ownerPassword: z.string().min(8, "A senha precisa ter ao menos 8 caracteres."),
});

type OnboardingFormValues = z.infer<typeof onboardingSchema>;

const STEPS = [
  { fields: ["businessName", "slug", "timeZoneId"], title: "Seu estabelecimento" },
  { fields: ["businessType"], title: "Qual e o seu segmento?" },
  { fields: ["ownerFullName", "ownerEmail", "ownerPassword"], title: "Sua conta" },
] as const satisfies ReadonlyArray<{ fields: ReadonlyArray<keyof OnboardingFormValues>; title: string }>;

const COMMON_TIME_ZONES = [
  "America/Sao_Paulo",
  "America/Manaus",
  "America/Rio_Branco",
  "America/Fortaleza",
  "America/Noronha",
];

export default function OnboardingPage() {
  const [step, setStep] = React.useState(0);
  const [slugTouched, setSlugTouched] = React.useState(false);
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [isResending, setIsResending] = React.useState(false);
  // Presente so apos o cadastro concluir — troca o formulario pela tela de
  // "confirme seu e-mail" em vez de logar automaticamente (login agora exige
  // e-mail confirmado).
  const [registered, setRegistered] = React.useState<{ tenantId: string; email: string } | null>(null);

  const businessTypesQuery = useQuery({
    queryKey: ["business-types"],
    queryFn: listBusinessTypes,
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
    setStep((s) => Math.max(s - 1, 0));
  }

  async function onSubmit(values: OnboardingFormValues) {
    setIsSubmitting(true);
    try {
      const tenant = await createTenant({
        name: values.businessName,
        slug: values.slug,
        businessType: values.businessType,
        timeZoneId: values.timeZoneId,
      });

      await registerUser({
        tenantId: tenant.id,
        email: values.ownerEmail,
        password: values.ownerPassword,
        fullName: values.ownerFullName,
      });

      setRegistered({ tenantId: tenant.id, email: values.ownerEmail });
    } catch (error) {
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

  if (registered) {
    return (
      <main className="flex min-h-full flex-1 items-center justify-center p-4 sm:p-6">
        <div className="w-full max-w-md text-center">
          <div className="mb-6 flex flex-col items-center gap-4">
            <Logo />
            <span className="bg-accent text-accent-foreground flex size-14 items-center justify-center rounded-full">
              <MailCheck className="size-7" aria-hidden />
            </span>
          </div>
          <h1 className="text-xl font-semibold tracking-tight">Confirme seu e-mail</h1>
          <p className="text-muted-foreground mt-2 text-sm text-balance">
            Enviamos um link de confirmação para <strong>{registered.email}</strong>. Clique nele para ativar
            sua conta e poder entrar.
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

  return (
    <main className="flex min-h-full flex-1 items-center justify-center p-4 sm:p-6">
      <div className="w-full max-w-lg">
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
                      <FormControl>
                        <select
                          {...field}
                          className="border-input bg-background flex h-9 w-full rounded-md border px-3 text-sm shadow-xs outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
                        >
                          {[...new Set([field.value, ...COMMON_TIME_ZONES])].map((tz) => (
                            <option key={tz} value={tz}>
                              {tz}
                            </option>
                          ))}
                        </select>
                      </FormControl>
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
              </>
            )}

            <div className="mt-2 flex items-center justify-between gap-3">
              <Button type="button" variant="ghost" onClick={goBack} disabled={step === 0 || isSubmitting}>
                Voltar
              </Button>
              {isLastStep ? (
                <Button key="submit" type="submit" disabled={isSubmitting}>
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
