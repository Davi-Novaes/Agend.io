"use client";

import * as React from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Camera } from "lucide-react";

import { getMyProfile, updateMyProfile, uploadUserAvatar, resolveAssetUrl, ApiError } from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";

function initialsFrom(fullName: string): string {
  const words = fullName.trim().split(/\s+/);
  const first = words[0]?.[0] ?? "";
  const last = words.length > 1 ? words[words.length - 1][0] : "";
  return `${first}${last}`.toUpperCase();
}

const profileSchema = z.object({
  fullName: z.string().trim().min(1, "Informe o nome."),
  phone: z.string().trim().optional(),
});
type ProfileFormValues = z.infer<typeof profileSchema>;

const MY_PROFILE_QUERY_KEY = ["my-profile"];

export default function AccountSettingsPage() {
  const { session } = useSession();
  const queryClient = useQueryClient();
  const accessToken = session?.accessToken ?? "";

  const [isUploadingAvatar, setIsUploadingAvatar] = React.useState(false);
  const [isSaving, setIsSaving] = React.useState(false);
  const avatarInputRef = React.useRef<HTMLInputElement>(null);

  const profileQuery = useQuery({
    queryKey: MY_PROFILE_QUERY_KEY,
    queryFn: () => getMyProfile(accessToken),
    enabled: Boolean(session),
  });

  const form = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    values: profileQuery.data
      ? { fullName: profileQuery.data.fullName, phone: profileQuery.data.phone ?? "" }
      : undefined,
    defaultValues: { fullName: "", phone: "" },
  });

  async function onAvatarSelected(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;

    setIsUploadingAvatar(true);
    try {
      await uploadUserAvatar(file, accessToken);
      await queryClient.invalidateQueries({ queryKey: MY_PROFILE_QUERY_KEY });
      toast.success("Foto de perfil atualizada.");
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Não foi possível enviar a foto.");
    } finally {
      setIsUploadingAvatar(false);
    }
  }

  async function onSubmit(values: ProfileFormValues) {
    setIsSaving(true);
    try {
      await updateMyProfile({ fullName: values.fullName, phone: values.phone || null }, accessToken);
      await queryClient.invalidateQueries({ queryKey: MY_PROFILE_QUERY_KEY });
      toast.success("Dados atualizados.");
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Não foi possível salvar.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <div className="flex w-full max-w-2xl flex-1 flex-col gap-6">
      <Card>
        <CardHeader>
          <CardTitle>Foto de perfil</CardTitle>
          <CardDescription>Aparece no menu do topo e onde mais o seu nome for exibido.</CardDescription>
        </CardHeader>
        <CardContent className="flex items-center gap-4">
          {profileQuery.isLoading ? (
            <Skeleton className="size-16 shrink-0 rounded-full" />
          ) : (
            <Avatar size="lg" className="size-16 shrink-0">
              {profileQuery.data?.avatarUrl && <AvatarImage src={resolveAssetUrl(profileQuery.data.avatarUrl)} alt="" />}
              <AvatarFallback className="text-base">
                {profileQuery.data ? initialsFrom(profileQuery.data.fullName) : "?"}
              </AvatarFallback>
            </Avatar>
          )}
          <div>
            <input
              ref={avatarInputRef}
              type="file"
              accept="image/png,image/jpeg,image/webp"
              className="hidden"
              onChange={onAvatarSelected}
            />
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={isUploadingAvatar}
              onClick={() => avatarInputRef.current?.click()}
            >
              <Camera className="size-4" />
              {isUploadingAvatar ? "Enviando..." : "Alterar foto"}
            </Button>
            <p className="text-muted-foreground mt-1.5 text-xs">PNG, JPEG ou WEBP, até 2MB.</p>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Dados pessoais</CardTitle>
          <CardDescription>Visualize e edite os seus dados. O e-mail de login não pode ser alterado por aqui.</CardDescription>
        </CardHeader>
        <CardContent>
          {profileQuery.isLoading ? (
            <div className="flex max-w-sm flex-col gap-3">
              <Skeleton className="h-9 w-full" />
              <Skeleton className="h-9 w-full" />
              <Skeleton className="h-9 w-full" />
            </div>
          ) : (
            <Form {...form}>
              <form onSubmit={form.handleSubmit(onSubmit)} className="flex max-w-sm flex-col gap-3">
                <FormField
                  control={form.control}
                  name="fullName"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Nome completo</FormLabel>
                      <FormControl>
                        <Input autoComplete="name" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="phone"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Telefone</FormLabel>
                      <FormControl>
                        <Input type="tel" autoComplete="tel" placeholder="+55 11 99999-9999" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormItem>
                  <FormLabel>E-mail</FormLabel>
                  <Input value={profileQuery.data?.email ?? ""} disabled readOnly />
                </FormItem>
                <Button type="submit" disabled={isSaving} className="mt-1 self-start">
                  {isSaving ? "Salvando..." : "Salvar alterações"}
                </Button>
              </form>
            </Form>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
