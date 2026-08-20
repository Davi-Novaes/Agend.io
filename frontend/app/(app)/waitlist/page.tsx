"use client";

import * as React from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Hourglass } from "lucide-react";

import {
  listWaitlist,
  convertWaitlistEntry,
  cancelWaitlistEntry,
  listResources,
  ApiError,
  type WaitlistEntry,
  type WaitlistStatus,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";

const PAGE_SIZE = 20;

const STATUS_LABELS: Record<WaitlistStatus, string> = {
  Waiting: "Aguardando",
  Notified: "Notificado",
  Converted: "Confirmado",
  Cancelled: "Cancelado",
};

const STATUS_VARIANTS: Record<WaitlistStatus, "secondary" | "info" | "success" | "destructive"> = {
  Waiting: "secondary",
  Notified: "info",
  Converted: "success",
  Cancelled: "destructive",
};

const TABS: { value: WaitlistStatus | "all"; label: string }[] = [
  { value: "all", label: "Todos" },
  { value: "Waiting", label: "Aguardando" },
  { value: "Notified", label: "Notificado" },
  { value: "Converted", label: "Confirmado" },
  { value: "Cancelled", label: "Cancelado" },
];

function formatDate(value: string): string {
  return new Date(`${value}T00:00:00`).toLocaleDateString("pt-BR");
}

function formatDateTime(value: string): string {
  return new Date(value).toLocaleString("pt-BR");
}

const convertSchema = z.object({
  resourceId: z.string().min(1, "Selecione um profissional."),
  startAtLocal: z.string().min(1, "Informe a data e o horario."),
});
type ConvertFormValues = z.infer<typeof convertSchema>;

export default function WaitlistPage() {
  const { session } = useSession();
  const queryClient = useQueryClient();
  const accessToken = session?.accessToken ?? "";

  const [statusFilter, setStatusFilter] = React.useState<WaitlistStatus | "all">("all");
  const [page, setPage] = React.useState(1);
  const [convertTarget, setConvertTarget] = React.useState<WaitlistEntry | null>(null);
  const [cancelTarget, setCancelTarget] = React.useState<WaitlistEntry | null>(null);

  const listQuery = useQuery({
    queryKey: ["waitlist", { statusFilter, page }],
    queryFn: () => listWaitlist({ status: statusFilter === "all" ? undefined : statusFilter, page, pageSize: PAGE_SIZE }, accessToken),
    enabled: Boolean(session),
    placeholderData: (previous) => previous,
  });

  const resourcesQuery = useQuery({
    queryKey: ["resources", "all"],
    queryFn: () => listResources({ page: 1, pageSize: 100 }, accessToken),
    enabled: Boolean(session) && Boolean(convertTarget),
  });

  const convertForm = useForm<ConvertFormValues>({
    resolver: zodResolver(convertSchema),
    defaultValues: { resourceId: "", startAtLocal: "" },
  });

  const convertMutation = useMutation({
    mutationFn: (values: ConvertFormValues) =>
      convertWaitlistEntry(convertTarget!.id, { resourceId: values.resourceId, startAtUtc: new Date(values.startAtLocal).toISOString() }, accessToken),
    onSuccess: () => {
      toast.success("Agendamento criado a partir da fila de espera.");
      queryClient.invalidateQueries({ queryKey: ["waitlist"] });
      setConvertTarget(null);
    },
    onError: (error) => {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel confirmar esta entrada.");
    },
  });

  const cancelMutation = useMutation({
    mutationFn: (id: string) => cancelWaitlistEntry(id, accessToken),
    onSuccess: () => {
      toast.success("Entrada removida da fila de espera.");
      queryClient.invalidateQueries({ queryKey: ["waitlist"] });
      setCancelTarget(null);
    },
    onError: (error) => {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel remover esta entrada.");
    },
  });

  const totalPages = Math.max(1, Math.ceil((listQuery.data?.totalCount ?? 0) / PAGE_SIZE));

  function openConvertDialog(entry: WaitlistEntry) {
    convertForm.reset({ resourceId: entry.resourceId ?? "", startAtLocal: "" });
    setConvertTarget(entry);
  }

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-1 flex-col">
      <div className="mb-6">
        <p className="text-muted-foreground text-sm">
          Clientes que entraram na fila quando nao havia horario disponivel. Ao cancelar um agendamento compativel, as entradas
          Aguardando sao notificadas por e-mail/WhatsApp — confirme aqui qual delas vira agendamento.
        </p>
      </div>

      <Tabs
        value={statusFilter}
        onValueChange={(value) => {
          setStatusFilter(value as WaitlistStatus | "all");
          setPage(1);
        }}
        className="mb-4"
      >
        <TabsList>
          {TABS.map((tab) => (
            <TabsTrigger key={tab.value} value={tab.value}>
              {tab.label}
            </TabsTrigger>
          ))}
        </TabsList>
      </Tabs>

      <Card>
        <CardContent className="flex flex-col gap-4">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Cliente</TableHead>
                <TableHead>Servico</TableHead>
                <TableHead>Profissional</TableHead>
                <TableHead>Data desejada</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Solicitado em</TableHead>
                <TableHead className="text-right">Acoes</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {listQuery.isLoading ? (
                Array.from({ length: 5 }).map((_, index) => (
                  <TableRow key={index}>
                    {Array.from({ length: 7 }).map((__, cellIndex) => (
                      <TableCell key={cellIndex}>
                        <Skeleton className="h-4 w-24" />
                      </TableCell>
                    ))}
                  </TableRow>
                ))
              ) : listQuery.data?.items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={7} className="p-0">
                    <EmptyState
                      icon={Hourglass}
                      title="Nenhuma entrada na fila de espera"
                      description="Quando um cliente entrar na fila pelo portal publico, ela aparece aqui."
                    />
                  </TableCell>
                </TableRow>
              ) : (
                listQuery.data?.items.map((entry) => (
                  <TableRow key={entry.id}>
                    <TableCell>
                      <p className="font-medium">{entry.customerName}</p>
                      <p className="text-muted-foreground text-xs">{entry.customerEmail ?? entry.customerPhone ?? "—"}</p>
                    </TableCell>
                    <TableCell>{entry.serviceName}</TableCell>
                    <TableCell>{entry.resourceName ?? "Qualquer profissional"}</TableCell>
                    <TableCell>{formatDate(entry.preferredDate)}</TableCell>
                    <TableCell>
                      <Badge variant={STATUS_VARIANTS[entry.status]}>{STATUS_LABELS[entry.status]}</Badge>
                    </TableCell>
                    <TableCell>{formatDateTime(entry.createdAtUtc)}</TableCell>
                    <TableCell className="text-right">
                      {(entry.status === "Waiting" || entry.status === "Notified") && (
                        <div className="flex justify-end gap-2">
                          <Button size="sm" onClick={() => openConvertDialog(entry)}>
                            Confirmar
                          </Button>
                          <Button size="sm" variant="outline" disabled={cancelMutation.isPending} onClick={() => setCancelTarget(entry)}>
                            Cancelar
                          </Button>
                        </div>
                      )}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>

          <div className="flex items-center justify-between text-sm">
            <span className="text-muted-foreground" aria-live="polite">
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

      <Dialog open={Boolean(convertTarget)} onOpenChange={(open) => !open && setConvertTarget(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Confirmar agendamento</DialogTitle>
          </DialogHeader>
          {convertTarget && (
            <p className="text-muted-foreground -mt-2 text-sm">
              {convertTarget.customerName} · {convertTarget.serviceName}
            </p>
          )}
          <Form {...convertForm}>
            <form onSubmit={convertForm.handleSubmit((values) => convertMutation.mutate(values))} className="flex flex-col gap-3">
              <FormField
                control={convertForm.control}
                name="resourceId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Profissional</FormLabel>
                    <Select value={field.value} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Selecione um profissional" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {(resourcesQuery.data?.items ?? []).map((resource) => (
                          <SelectItem key={resource.id} value={resource.id}>
                            {resource.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={convertForm.control}
                name="startAtLocal"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Data e horario</FormLabel>
                    <FormControl>
                      <Input type="datetime-local" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <DialogFooter>
                <Button type="submit" disabled={convertMutation.isPending}>
                  {convertMutation.isPending ? "Confirmando..." : "Confirmar e agendar"}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      <AlertDialog open={cancelTarget !== null} onOpenChange={(open) => !open && setCancelTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Cancelar entrada na lista de espera?</AlertDialogTitle>
            <AlertDialogDescription>
              {cancelTarget &&
                `Remove "${cancelTarget.customerName}" da fila de espera para ${cancelTarget.serviceName}. Essa acao nao pode ser desfeita.`}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Voltar</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              disabled={cancelMutation.isPending}
              onClick={() => {
                if (cancelTarget) {
                  cancelMutation.mutate(cancelTarget.id);
                }
              }}
            >
              {cancelMutation.isPending ? "Cancelando..." : "Cancelar entrada"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
