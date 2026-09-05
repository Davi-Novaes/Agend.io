"use client";

import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm, useFieldArray } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Plus, Trash2 } from "lucide-react";

import {
  getTenantProfile,
  updateTenantProfile,
  setTenantBusinessHours,
  updateTenantSchedulingSettings,
  TENANT_PROFILE_QUERY_KEY,
  ApiError,
  type DayOfWeekName,
  type TenantProfile,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { useBrandingDraft } from "@/components/settings/branding/branding-draft-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";

const DAY_OPTIONS: { value: DayOfWeekName; label: string }[] = [
  { value: "Monday", label: "Segunda" },
  { value: "Tuesday", label: "Terça" },
  { value: "Wednesday", label: "Quarta" },
  { value: "Thursday", label: "Quinta" },
  { value: "Friday", label: "Sexta" },
  { value: "Saturday", label: "Sábado" },
  { value: "Sunday", label: "Domingo" },
];

const profileSchema = z.object({
  description: z.string(),
  phone: z.string(),
  whatsApp: z.string(),
  email: z.string(),
  address: z.string(),
  instagramUrl: z.string(),
  facebookUrl: z.string(),
});
type ProfileFormValues = z.infer<typeof profileSchema>;

const businessHoursSchema = z.object({
  entries: z
    .array(
      z.object({
        dayOfWeek: z.enum(["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"]),
        startTime: z.string().min(1, "Informe o horário inicial."),
        endTime: z.string().min(1, "Informe o horário final."),
      })
    )
    // Dois turnos no MESMO dia (manha/tarde) sao validos de proposito -- so
    // rejeita quando dia+inicio+fim se repetem identicos.
    .refine(
      (entries) => {
        const seen = new Set<string>();
        for (const entry of entries) {
          const key = `${entry.dayOfWeek}|${entry.startTime}|${entry.endTime}`;
          if (seen.has(key)) return false;
          seen.add(key);
        }
        return true;
      },
      { message: "Há horários duplicados (mesmo dia e mesmo intervalo) na lista." }
    ),
});
type BusinessHoursFormValues = z.infer<typeof businessHoursSchema>;

const schedulingSettingsSchema = z.object({
  closedDates: z.array(z.object({ date: z.string().min(1, "Informe a data."), reason: z.string() })),
  appointmentBufferMinutes: z.coerce.number().int().min(0, "Mínimo 0.").max(240, "Máximo 240."),
});
type SchedulingSettingsFormInput = z.input<typeof schedulingSettingsSchema>;
type SchedulingSettingsFormValues = z.output<typeof schedulingSettingsSchema>;

function toTimeInputValue(time: string): string {
  return time.slice(0, 5);
}
function toApiTimeValue(time: string): string {
  return time.length === 5 ? `${time}:00` : time;
}
function toNullable(value: string): string | null {
  return value.trim() === "" ? null : value.trim();
}

export default function BrandingInfoPage() {
  const { session } = useSession();
  const accessToken = session?.accessToken ?? "";
  const queryClient = useQueryClient();
  const { update } = useBrandingDraft();

  const profileQuery = useQuery({
    queryKey: TENANT_PROFILE_QUERY_KEY,
    queryFn: () => getTenantProfile(accessToken),
    enabled: Boolean(session),
  });

  if (profileQuery.isLoading || !profileQuery.data) {
    return (
      <div className="flex flex-col gap-6">
        <Skeleton className="h-72 w-full" />
        <Skeleton className="h-56 w-full" />
        <Skeleton className="h-56 w-full" />
      </div>
    );
  }

  const profile = profileQuery.data;

  return <InfoForms profile={profile} accessToken={accessToken} onUpdateDraft={update} onSaved={() => queryClient.invalidateQueries({ queryKey: TENANT_PROFILE_QUERY_KEY })} />;
}

function InfoForms({
  profile,
  accessToken,
  onUpdateDraft,
  onSaved,
}: {
  profile: TenantProfile;
  accessToken: string;
  onUpdateDraft: (patch: { description: string }) => void;
  onSaved: () => void;
}) {
  const profileForm = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: {
      description: profile.description ?? "",
      phone: profile.phone ?? "",
      whatsApp: profile.whatsApp ?? "",
      email: profile.email ?? "",
      address: profile.address ?? "",
      instagramUrl: profile.instagramUrl ?? "",
      facebookUrl: profile.facebookUrl ?? "",
    },
  });

  const hoursForm = useForm<BusinessHoursFormValues>({
    resolver: zodResolver(businessHoursSchema),
    defaultValues: {
      entries: profile.businessHours.map((entry) => ({
        dayOfWeek: entry.dayOfWeek,
        startTime: toTimeInputValue(entry.startTime),
        endTime: toTimeInputValue(entry.endTime),
      })),
    },
  });
  const hoursFieldArray = useFieldArray({ control: hoursForm.control, name: "entries" });

  const schedulingSettingsForm = useForm<SchedulingSettingsFormInput, unknown, SchedulingSettingsFormValues>({
    resolver: zodResolver(schedulingSettingsSchema),
    defaultValues: {
      closedDates: profile.closedDates.map((entry) => ({ date: entry.date, reason: entry.reason ?? "" })),
      appointmentBufferMinutes: profile.appointmentBufferMinutes,
    },
  });
  const closedDatesFieldArray = useFieldArray({ control: schedulingSettingsForm.control, name: "closedDates" });

  const profileMutation = useMutation({
    mutationFn: (values: ProfileFormValues) =>
      updateTenantProfile(
        {
          description: toNullable(values.description),
          phone: toNullable(values.phone),
          whatsApp: toNullable(values.whatsApp),
          email: toNullable(values.email),
          address: toNullable(values.address),
          instagramUrl: toNullable(values.instagramUrl),
          facebookUrl: toNullable(values.facebookUrl),
        },
        accessToken
      ),
    onSuccess: () => {
      toast.success("Informações atualizadas.");
      onSaved();
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Não foi possível salvar."),
  });

  const businessHoursMutation = useMutation({
    mutationFn: (values: BusinessHoursFormValues) => {
      const entries = values.entries.map((entry) => ({ ...entry, startTime: toApiTimeValue(entry.startTime), endTime: toApiTimeValue(entry.endTime) }));
      return setTenantBusinessHours(entries, accessToken);
    },
    onSuccess: () => {
      toast.success("Horário de funcionamento atualizado.");
      onSaved();
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Não foi possível salvar o horário."),
  });

  const schedulingSettingsMutation = useMutation({
    mutationFn: (values: SchedulingSettingsFormValues) =>
      updateTenantSchedulingSettings(
        { closedDates: values.closedDates.map((entry) => ({ date: entry.date, reason: toNullable(entry.reason) })), appointmentBufferMinutes: values.appointmentBufferMinutes },
        accessToken
      ),
    onSuccess: () => {
      toast.success("Configurações de agendamento atualizadas.");
      onSaved();
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Não foi possível salvar as configurações de agendamento."),
  });

  return (
    <div className="flex flex-col gap-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Contato e redes sociais</CardTitle>
          <CardDescription>
            Aparece no portal público do seu estabelecimento. Dados cadastrais (CNPJ/CPF, razão social) ficam em{" "}
            <Link href="/settings/company" className="underline underline-offset-2">
              Empresa → Dados da empresa
            </Link>
            .
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Form {...profileForm}>
            <form
              onSubmit={profileForm.handleSubmit((values) => {
                onUpdateDraft({ description: values.description });
                profileMutation.mutate(values);
              })}
              className="grid gap-4"
            >
              <FormField
                control={profileForm.control}
                name="description"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Descrição</FormLabel>
                    <FormControl>
                      <Textarea rows={3} placeholder="Conte um pouco sobre o seu negócio." {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <div className="grid gap-4 sm:grid-cols-2">
                <FormField
                  control={profileForm.control}
                  name="phone"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Telefone</FormLabel>
                      <FormControl>
                        <Input placeholder="(11) 99999-8888" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={profileForm.control}
                  name="whatsApp"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>WhatsApp</FormLabel>
                      <FormControl>
                        <Input placeholder="(11) 99999-8888" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>
              <FormField
                control={profileForm.control}
                name="email"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>E-mail de contato</FormLabel>
                    <FormControl>
                      <Input type="email" placeholder="contato@seunegocio.com" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={profileForm.control}
                name="address"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Endereço</FormLabel>
                    <FormControl>
                      <Input placeholder="Rua das Flores, 100 - Centro" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <div className="grid gap-4 sm:grid-cols-2">
                <FormField
                  control={profileForm.control}
                  name="instagramUrl"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Instagram</FormLabel>
                      <FormControl>
                        <Input placeholder="https://instagram.com/seunegocio" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={profileForm.control}
                  name="facebookUrl"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Facebook</FormLabel>
                      <FormControl>
                        <Input placeholder="https://facebook.com/seunegocio" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>
              <Button type="submit" disabled={profileMutation.isPending} className="w-fit">
                {profileMutation.isPending ? "Salvando..." : "Salvar contato"}
              </Button>
            </form>
          </Form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Horário de funcionamento</CardTitle>
          <CardDescription>Usado no portal público para mostrar quando o estabelecimento está aberto.</CardDescription>
        </CardHeader>
        <CardContent>
          <Form {...hoursForm}>
            <form onSubmit={hoursForm.handleSubmit((values) => businessHoursMutation.mutate(values))} className="flex flex-col gap-3">
              {hoursFieldArray.fields.length === 0 && <p className="text-muted-foreground text-sm">Nenhum horário cadastrado ainda.</p>}
              {hoursFieldArray.fields.map((field, index) => (
                <div key={field.id} className="flex items-end gap-2">
                  <FormField
                    control={hoursForm.control}
                    name={`entries.${index}.dayOfWeek`}
                    render={({ field: dayField }) => (
                      <FormItem className="flex-1">
                        <FormLabel className={index === 0 ? undefined : "sr-only"}>Dia</FormLabel>
                        <Select value={dayField.value} onValueChange={dayField.onChange}>
                          <FormControl>
                            <SelectTrigger className="w-full">
                              <SelectValue />
                            </SelectTrigger>
                          </FormControl>
                          <SelectContent>
                            {DAY_OPTIONS.map((day) => (
                              <SelectItem key={day.value} value={day.value}>
                                {day.label}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={hoursForm.control}
                    name={`entries.${index}.startTime`}
                    render={({ field: startField }) => (
                      <FormItem>
                        <FormLabel className={index === 0 ? undefined : "sr-only"}>Início</FormLabel>
                        <FormControl>
                          <Input type="time" {...startField} />
                        </FormControl>
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={hoursForm.control}
                    name={`entries.${index}.endTime`}
                    render={({ field: endField }) => (
                      <FormItem>
                        <FormLabel className={index === 0 ? undefined : "sr-only"}>Fim</FormLabel>
                        <FormControl>
                          <Input type="time" {...endField} />
                        </FormControl>
                      </FormItem>
                    )}
                  />
                  <Button type="button" variant="ghost" size="icon" aria-label="Remover horário" onClick={() => hoursFieldArray.remove(index)}>
                    <Trash2 className="size-4" />
                  </Button>
                </div>
              ))}
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="self-start"
                onClick={() => hoursFieldArray.append({ dayOfWeek: "Monday", startTime: "09:00", endTime: "18:00" })}
              >
                <Plus className="size-4" />
                Adicionar horário
              </Button>
              {hoursForm.formState.errors.entries?.message && (
                <p role="alert" className="text-destructive text-sm">
                  {hoursForm.formState.errors.entries.message}
                </p>
              )}
              <Button type="submit" disabled={businessHoursMutation.isPending} className="w-fit">
                {businessHoursMutation.isPending ? "Salvando..." : "Salvar horários"}
              </Button>
            </form>
          </Form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Datas fechadas e intervalo entre horários</CardTitle>
          <CardDescription>Feriados e eventos em que o estabelecimento não atende, e um intervalo mínimo de descanso entre agendamentos.</CardDescription>
        </CardHeader>
        <CardContent>
          <Form {...schedulingSettingsForm}>
            <form onSubmit={schedulingSettingsForm.handleSubmit((values) => schedulingSettingsMutation.mutate(values))} className="flex flex-col gap-6">
              <div className="flex flex-col gap-3">
                <Label>Datas fechadas (feriados e eventos)</Label>
                {closedDatesFieldArray.fields.length === 0 && <p className="text-muted-foreground text-sm">Nenhuma data fechada cadastrada ainda.</p>}
                {closedDatesFieldArray.fields.map((field, index) => (
                  <div key={field.id} className="flex flex-col gap-2 sm:flex-row sm:items-end">
                    <FormField
                      control={schedulingSettingsForm.control}
                      name={`closedDates.${index}.date`}
                      render={({ field: dateField }) => (
                        <FormItem className="sm:w-40">
                          <FormLabel className={index === 0 ? undefined : "sr-only"}>Data</FormLabel>
                          <FormControl>
                            <Input type="date" className="w-full" {...dateField} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <div className="flex items-end gap-2">
                      <FormField
                        control={schedulingSettingsForm.control}
                        name={`closedDates.${index}.reason`}
                        render={({ field: reasonField }) => (
                          <FormItem className="flex-1">
                            <FormLabel className={index === 0 ? undefined : "sr-only"}>Motivo (opcional)</FormLabel>
                            <FormControl>
                              <Input placeholder="Ex.: Natal, reforma" {...reasonField} />
                            </FormControl>
                          </FormItem>
                        )}
                      />
                      <Button type="button" variant="ghost" size="icon" aria-label="Remover data fechada" onClick={() => closedDatesFieldArray.remove(index)}>
                        <Trash2 className="size-4" />
                      </Button>
                    </div>
                  </div>
                ))}
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="self-start"
                  onClick={() => closedDatesFieldArray.append({ date: "", reason: "" })}
                >
                  <Plus className="size-4" />
                  Adicionar data fechada
                </Button>
              </div>

              <FormField
                control={schedulingSettingsForm.control}
                name="appointmentBufferMinutes"
                render={({ field: bufferField }) => (
                  <FormItem className="max-w-xs">
                    <FormLabel>Intervalo mínimo entre agendamentos (minutos)</FormLabel>
                    <FormControl>
                      <Input type="number" min={0} max={240} {...bufferField} value={bufferField.value as number} />
                    </FormControl>
                    <FormDescription>
                      Tempo de folga exigido antes e depois de um agendamento existente. Só tem efeito perto de horários já ocupados.
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <Button type="submit" disabled={schedulingSettingsMutation.isPending} className="w-fit">
                {schedulingSettingsMutation.isPending ? "Salvando..." : "Salvar configurações de agendamento"}
              </Button>
            </form>
          </Form>
        </CardContent>
      </Card>
    </div>
  );
}
