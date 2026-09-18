"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";

import { submitFeedback, ApiError } from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";

const feedbackSchema = z.object({
  subject: z.string().trim().min(1, "Informe o assunto."),
  body: z.string().trim().min(1, "Escreva sua mensagem."),
});
type FeedbackFormValues = z.infer<typeof feedbackSchema>;

export default function FeedbackSettingsPage() {
  const { session } = useSession();
  const accessToken = session?.accessToken ?? "";

  const form = useForm<FeedbackFormValues>({
    resolver: zodResolver(feedbackSchema),
    defaultValues: { subject: "", body: "" },
  });

  const mutation = useMutation({
    mutationFn: (values: FeedbackFormValues) => submitFeedback(values, accessToken),
    onSuccess: () => {
      toast.success("Feedback enviado. Obrigado!");
      form.reset({ subject: "", body: "" });
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Não foi possível enviar o feedback."),
  });

  return (
    <div className="flex w-full max-w-2xl flex-1 flex-col gap-6">
      <div>
        <h1 className="text-lg font-semibold">Feedback</h1>
        <p className="text-muted-foreground text-sm">Sugestões, elogios ou problemas que você encontrou no sistema.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Enviar feedback</CardTitle>
          <CardDescription>Qualquer pessoa da equipe pode enviar.</CardDescription>
        </CardHeader>
        <CardContent>
          <Form {...form}>
            <form onSubmit={form.handleSubmit((values) => mutation.mutate(values))} className="grid max-w-md gap-4">
              <FormField
                control={form.control}
                name="subject"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Assunto</FormLabel>
                    <FormControl>
                      <Input {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="body"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Mensagem</FormLabel>
                    <FormControl>
                      <Textarea rows={6} {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <Button type="submit" disabled={mutation.isPending} className="w-fit">
                {mutation.isPending ? "Enviando..." : "Enviar feedback"}
              </Button>
            </form>
          </Form>
        </CardContent>
      </Card>
    </div>
  );
}
