"use client";

import * as React from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Megaphone, Send } from "lucide-react";

import { sendCampaign, listCampaigns, ApiError, type CampaignChannel, type CustomerSegment } from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent } from "@/components/ui/card";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";

const PAGE_SIZE = 20;

function formatDateTime(value: string): string {
  return new Date(value).toLocaleString("pt-BR");
}

const CHANNEL_LABEL: Record<CampaignChannel, string> = {
  Email: "E-mail",
  WhatsApp: "WhatsApp",
};

// Mesmos rotulos de /clientes (Fase 9) — auto-segmentacao calculada no backend.
const SEGMENT_LABEL: Record<CustomerSegment, string> = {
  Novo: "Novo",
  Recorrente: "Recorrente",
  Vip: "VIP",
  EmRisco: "Em risco",
  Inativo: "Inativo",
  NoShow: "Faltas",
};
const SEGMENTS: CustomerSegment[] = ["Novo", "Recorrente", "Vip", "EmRisco", "Inativo", "NoShow"];

const campaignSchema = z.object({
  subject: z.string().min(1, "Informe o assunto."),
  body: z.string().min(1, "Informe a mensagem."),
  channel: z.enum(["Email", "WhatsApp"]),
  targetSegment: z.union([z.enum(["Novo", "Recorrente", "Vip", "EmRisco", "Inativo", "NoShow"]), z.literal("all")]),
});
type CampaignFormValues = z.infer<typeof campaignSchema>;
const emptyCampaignForm: CampaignFormValues = { subject: "", body: "", channel: "Email", targetSegment: "all" };

export default function MarketingPage() {
  const { session } = useSession();
  const queryClient = useQueryClient();

  const [page, setPage] = React.useState(1);
  const [formDialogOpen, setFormDialogOpen] = React.useState(false);
  const [confirmDialogOpen, setConfirmDialogOpen] = React.useState(false);
  const [pendingCampaign, setPendingCampaign] = React.useState<CampaignFormValues | null>(null);

  const accessToken = session?.accessToken ?? "";

  const listQuery = useQuery({
    queryKey: ["marketing", "campanhas", { page }],
    queryFn: () => listCampaigns({ page, pageSize: PAGE_SIZE }, accessToken),
    enabled: Boolean(session),
    placeholderData: (previous) => previous,
  });

  const form = useForm<CampaignFormValues>({
    resolver: zodResolver(campaignSchema),
    defaultValues: emptyCampaignForm,
  });

  const sendMutation = useMutation({
    mutationFn: (values: CampaignFormValues) =>
      sendCampaign(
        {
          subject: values.subject,
          body: values.body,
          channel: values.channel,
          targetSegment: values.targetSegment === "all" ? null : values.targetSegment,
        },
        accessToken
      ),
    onSuccess: (data) => {
      toast.success(`Campanha enviada para ${data.recipientCount} cliente(s).`);
      queryClient.invalidateQueries({ queryKey: ["marketing", "campanhas"] });
      setConfirmDialogOpen(false);
      setFormDialogOpen(false);
      form.reset(emptyCampaignForm);
      setPendingCampaign(null);
    },
    onError: (error) => {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel enviar a campanha.");
      setConfirmDialogOpen(false);
    },
  });

  const totalPages = Math.max(1, Math.ceil((listQuery.data?.totalCount ?? 0) / PAGE_SIZE));

  function openCreateDialog() {
    form.reset(emptyCampaignForm);
    setFormDialogOpen(true);
  }

  function handleReviewSubmit(values: CampaignFormValues) {
    setPendingCampaign(values);
    setFormDialogOpen(false);
    setConfirmDialogOpen(true);
  }

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-1 flex-col">
      <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
        <p className="text-muted-foreground text-sm">Campanhas por e-mail ou WhatsApp para os clientes ativos.</p>
        <Button onClick={openCreateDialog}>
          <Send className="size-4" />
          Nova campanha
        </Button>
      </div>

      <Card>
        <CardContent className="flex flex-col gap-4">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Assunto</TableHead>
                <TableHead>Canal</TableHead>
                <TableHead>Publico</TableHead>
                <TableHead>Enviada em</TableHead>
                <TableHead className="text-right">Destinatarios</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {listQuery.isLoading ? (
                Array.from({ length: 5 }).map((_, index) => (
                  <TableRow key={index}>
                    <TableCell><Skeleton className="h-4 w-48" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-20" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-20" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-32" /></TableCell>
                    <TableCell className="text-right"><Skeleton className="ml-auto h-4 w-16" /></TableCell>
                  </TableRow>
                ))
              ) : listQuery.data?.items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} className="p-0">
                    <EmptyState
                      icon={Megaphone}
                      title="Nenhuma campanha enviada ainda"
                      description="Envie sua primeira campanha por e-mail ou WhatsApp para os clientes ativos."
                      action={
                        <Button size="sm" onClick={openCreateDialog}>
                          <Send className="size-4" />
                          Nova campanha
                        </Button>
                      }
                    />
                  </TableCell>
                </TableRow>
              ) : (
                listQuery.data?.items.map((campaign) => (
                  <TableRow key={campaign.id}>
                    <TableCell className="font-medium">{campaign.subject}</TableCell>
                    <TableCell>{CHANNEL_LABEL[campaign.channel]}</TableCell>
                    <TableCell>{campaign.targetSegment ? SEGMENT_LABEL[campaign.targetSegment] : "Todos"}</TableCell>
                    <TableCell>{formatDateTime(campaign.sentAtUtc)}</TableCell>
                    <TableCell className="text-right tabular-nums">{campaign.recipientCount}</TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>

          <div className="flex items-center justify-between text-sm">
            <span className="text-muted-foreground">
              Pagina {listQuery.data?.page ?? page} de {totalPages}
            </span>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((current) => current - 1)}>
                Anterior
              </Button>
              <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage((current) => current + 1)}>
                Proxima
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <Dialog open={formDialogOpen} onOpenChange={setFormDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Nova campanha</DialogTitle>
          </DialogHeader>
          <Form {...form}>
            <form onSubmit={form.handleSubmit(handleReviewSubmit)} className="flex flex-col gap-3">
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
              <div className="grid grid-cols-2 gap-3">
                <FormField
                  control={form.control}
                  name="channel"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Canal</FormLabel>
                      <Select value={field.value} onValueChange={field.onChange}>
                        <FormControl>
                          <SelectTrigger className="w-full">
                            <SelectValue />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          <SelectItem value="Email">E-mail</SelectItem>
                          <SelectItem value="WhatsApp">WhatsApp</SelectItem>
                        </SelectContent>
                      </Select>
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="targetSegment"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Publico</FormLabel>
                      <Select value={field.value} onValueChange={field.onChange}>
                        <FormControl>
                          <SelectTrigger className="w-full">
                            <SelectValue />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          <SelectItem value="all">Todos os clientes</SelectItem>
                          {SEGMENTS.map((segment) => (
                            <SelectItem key={segment} value={segment}>
                              {SEGMENT_LABEL[segment]}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </FormItem>
                  )}
                />
              </div>
              <FormField
                control={form.control}
                name="body"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Mensagem</FormLabel>
                    <FormControl>
                      <Textarea rows={8} {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <DialogFooter>
                <Button type="submit">Revisar envio</Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      <Dialog open={confirmDialogOpen} onOpenChange={setConfirmDialogOpen}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader>
            <DialogTitle>Confirmar envio</DialogTitle>
          </DialogHeader>
          <p className="text-sm">
            Enviar a campanha <strong>&ldquo;{pendingCampaign?.subject}&rdquo;</strong> por{" "}
            <strong>{pendingCampaign ? CHANNEL_LABEL[pendingCampaign.channel] : ""}</strong> para{" "}
            <strong>
              {pendingCampaign?.targetSegment && pendingCampaign.targetSegment !== "all"
                ? `clientes ativos no segmento "${SEGMENT_LABEL[pendingCampaign.targetSegment]}"`
                : "todos os clientes ativos"}
            </strong>{" "}
            elegiveis para o canal escolhido? Essa acao nao pode ser desfeita.
          </p>
          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => {
                setConfirmDialogOpen(false);
                setFormDialogOpen(true);
              }}
            >
              Voltar
            </Button>
            <Button type="button" disabled={sendMutation.isPending} onClick={() => pendingCampaign && sendMutation.mutate(pendingCampaign)}>
              {sendMutation.isPending ? "Enviando..." : "Confirmar e enviar"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
