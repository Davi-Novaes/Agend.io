"use client";

import * as React from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import Link from "next/link";
import { Plus, Inbox, ListFilter, Receipt, Users, AlertTriangle } from "lucide-react";

import {
  listAccountsReceivable,
  markAccountReceivableReceived,
  listAccountsPayable,
  createAccountPayable,
  markAccountPayablePaid,
  cancelAccountPayable,
  listCommissionRules,
  upsertCommissionRule,
  deactivateCommissionRule,
  getCashFlowSummary,
  ApiError,
  type AccountReceivableStatus,
  type AccountReceivableSummary,
  type AccountPayableStatus,
  type AccountPayableSummary,
  type ExpenseCategory,
  type CommissionRuleSummary,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { CashFlowChart, CATEGORY_LABELS } from "@/components/financeiro/cash-flow-chart";
import { PeriodFilter } from "@/components/shared/period-filter";
import { toDateOnly, startOfMonth } from "@/lib/date-utils";

const PAGE_SIZE = 20;

const RECEIVABLE_STATUS_LABELS: Record<AccountReceivableStatus, string> = {
  Pending: "Pendente",
  Received: "Recebido",
  Cancelled: "Cancelado",
};

const PAYABLE_STATUS_LABELS: Record<AccountPayableStatus, string> = {
  Pending: "Pendente",
  Paid: "Pago",
  Cancelled: "Cancelado",
};

const RECEIVABLE_STATUS_VARIANTS: Record<AccountReceivableStatus, "secondary" | "success" | "destructive"> = {
  Pending: "secondary",
  Received: "success",
  Cancelled: "destructive",
};

const PAYABLE_STATUS_VARIANTS: Record<AccountPayableStatus, "secondary" | "success" | "destructive"> = {
  Pending: "secondary",
  Paid: "success",
  Cancelled: "destructive",
};

const EXPENSE_CATEGORIES: ExpenseCategory[] = ["Rent", "Supplies", "Commission", "Salary", "Utilities", "Marketing", "Other"];

function formatCurrency(value: number): string {
  return value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

function formatDate(value: string): string {
  return new Date(`${value.slice(0, 10)}T00:00:00`).toLocaleDateString("pt-BR");
}

const expenseSchema = z.object({
  description: z.string().min(1, "Informe a descricao."),
  amount: z.coerce.number().positive("Informe um valor maior que zero."),
  dueDate: z.string().min(1, "Informe a data."),
  category: z.enum(["Rent", "Supplies", "Commission", "Salary", "Utilities", "Marketing", "Other"]),
});
type ExpenseFormInput = z.input<typeof expenseSchema>;
type ExpenseFormValues = z.output<typeof expenseSchema>;
const emptyExpenseForm: ExpenseFormInput = { description: "", amount: 0, dueDate: toDateOnly(new Date()), category: "Other" };

const commissionSchema = z.object({
  calculationType: z.enum(["Percentage", "FixedAmount"]),
  value: z.coerce.number().min(0, "O valor nao pode ser negativo."),
});
type CommissionFormInput = z.input<typeof commissionSchema>;
type CommissionFormValues = z.output<typeof commissionSchema>;

export default function FinanceiroPage() {
  const { session } = useSession();
  const [tab, setTab] = React.useState("resumo");

  if (!session) {
    return null;
  }

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-1 flex-col">
      <div className="mb-6">
        <p className="text-muted-foreground text-sm">Contas a pagar, a receber, comissoes e fluxo de caixa.</p>
      </div>

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="resumo">Resumo</TabsTrigger>
          <TabsTrigger value="receber">Contas a Receber</TabsTrigger>
          <TabsTrigger value="pagar">Contas a Pagar</TabsTrigger>
          <TabsTrigger value="comissoes">Comissoes</TabsTrigger>
        </TabsList>

        <TabsContent value="resumo" className="mt-4">
          <ResumoTab accessToken={session.accessToken} onNavigateToPagar={() => setTab("pagar")} />
        </TabsContent>
        <TabsContent value="receber" className="mt-4">
          <ContasAReceberTab accessToken={session.accessToken} />
        </TabsContent>
        <TabsContent value="pagar" className="mt-4">
          <ContasAPagarTab accessToken={session.accessToken} />
        </TabsContent>
        <TabsContent value="comissoes" className="mt-4">
          <ComissoesTab accessToken={session.accessToken} />
        </TabsContent>
      </Tabs>
    </div>
  );
}

function ResumoTab({ accessToken, onNavigateToPagar }: { accessToken: string; onNavigateToPagar: () => void }) {
  const [from, setFrom] = React.useState(() => toDateOnly(startOfMonth(new Date())));
  const [to, setTo] = React.useState(() => toDateOnly(new Date()));

  const summaryQuery = useQuery({
    queryKey: ["financeiro", "fluxo-de-caixa", from, to],
    queryFn: () => getCashFlowSummary({ from, to }, accessToken),
  });

  // O Resumo so soma o que ja foi PAGO (fluxo de caixa realizado) — sem isso,
  // uma despesa recem-lancada como "Pendente" nao aparece em lugar nenhum
  // aqui, dando a falsa impressao de que o lancamento nao funcionou
  // (confirmado ao vivo pela Persona C). BL-15, docs/BACKLOG.md.
  const pendingPayablesQuery = useQuery({
    queryKey: ["financeiro", "contas-a-pagar-pendentes"],
    queryFn: () => listAccountsPayable({ status: "Pending", pageSize: 100 }, accessToken),
  });

  const summary = summaryQuery.data;
  const pendingItems = pendingPayablesQuery.data?.items ?? [];
  const pendingTotal = pendingItems.reduce((sum, item) => sum + item.amount, 0);

  return (
    <div className="flex flex-col gap-4">
      <PeriodFilter from={from} to={to} onFromChange={setFrom} onToChange={setTo} />

      {pendingItems.length > 0 && (
        <div className="border-warning/50 bg-warning/10 flex items-center justify-between gap-3 rounded-lg border p-4 text-sm">
          <div className="flex items-center gap-3">
            <AlertTriangle className="text-warning size-5 shrink-0" aria-hidden="true" />
            <p>
              Voce tem <span className="font-medium">{formatCurrency(pendingTotal)}</span> em{" "}
              {pendingItems.length === 1 ? "1 conta a pagar pendente" : `${pendingItems.length} contas a pagar pendentes`} —
              esse valor ainda nao entra nas Saidas/Saldo abaixo (so contam contas ja pagas).
            </p>
          </div>
          <Button variant="outline" size="sm" onClick={onNavigateToPagar} className="shrink-0">
            Ver contas a pagar
          </Button>
        </div>
      )}

      <div className="grid gap-4 sm:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle className="text-muted-foreground text-sm font-normal">Entradas</CardTitle>
          </CardHeader>
          <CardContent>
            {summaryQuery.isLoading ? (
              <Skeleton className="h-8 w-32" />
            ) : (
              <p className="text-2xl font-semibold">{summary ? formatCurrency(summary.totalReceived) : "—"}</p>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="text-muted-foreground text-sm font-normal">Saidas</CardTitle>
          </CardHeader>
          <CardContent>
            {summaryQuery.isLoading ? (
              <Skeleton className="h-8 w-32" />
            ) : (
              <p className="text-2xl font-semibold">{summary ? formatCurrency(summary.totalPaid) : "—"}</p>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="text-muted-foreground text-sm font-normal">Saldo</CardTitle>
          </CardHeader>
          <CardContent>
            {summaryQuery.isLoading ? (
              <Skeleton className="h-8 w-32" />
            ) : (
              <p className="text-2xl font-semibold">{summary ? formatCurrency(summary.netBalance) : "—"}</p>
            )}
          </CardContent>
        </Card>
      </div>

      {summaryQuery.isLoading ? (
        <Skeleton className="h-64 rounded-lg lg:col-span-2" />
      ) : summary ? (
        <CashFlowChart seriesByMonth={summary.seriesByMonth} categoryBreakdown={summary.categoryBreakdown} />
      ) : null}
    </div>
  );
}

function ContasAReceberTab({ accessToken }: { accessToken: string }) {
  const queryClient = useQueryClient();
  const [page, setPage] = React.useState(1);
  const [status, setStatus] = React.useState<AccountReceivableStatus | "all">("all");
  const [confirmingReceipt, setConfirmingReceipt] = React.useState<AccountReceivableSummary | null>(null);

  const listQuery = useQuery({
    queryKey: ["financeiro", "contas-a-receber", { page, status }],
    queryFn: () => listAccountsReceivable({ page, pageSize: PAGE_SIZE, status: status === "all" ? undefined : status }, accessToken),
    placeholderData: (previous) => previous,
  });

  const receiveMutation = useMutation({
    mutationFn: (id: string) => markAccountReceivableReceived(id, accessToken),
    onSuccess: () => {
      toast.success("Recebimento confirmado.");
      queryClient.invalidateQueries({ queryKey: ["financeiro", "contas-a-receber"] });
      queryClient.invalidateQueries({ queryKey: ["financeiro", "fluxo-de-caixa"] });
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel confirmar o recebimento."),
  });

  const totalPages = Math.max(1, Math.ceil((listQuery.data?.totalCount ?? 0) / PAGE_SIZE));

  return (
    <Card>
      <CardContent className="flex flex-col gap-4">
      <div className="flex justify-end">
        <Select value={status} onValueChange={(value) => { setStatus(value as AccountReceivableStatus | "all"); setPage(1); }}>
          <SelectTrigger className="w-44">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Todos os status</SelectItem>
            {Object.entries(RECEIVABLE_STATUS_LABELS).map(([value, label]) => (
              <SelectItem key={value} value={value}>
                {label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Descricao</TableHead>
              <TableHead>Vencimento</TableHead>
              <TableHead className="text-right">Valor</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Acoes</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {listQuery.isLoading ? (
              Array.from({ length: 5 }).map((_, index) => (
                <TableRow key={index}>
                  <TableCell><Skeleton className="h-4 w-40" /></TableCell>
                  <TableCell><Skeleton className="h-4 w-24" /></TableCell>
                  <TableCell className="text-right"><Skeleton className="ml-auto h-4 w-24" /></TableCell>
                  <TableCell><Skeleton className="h-5 w-20 rounded-full" /></TableCell>
                  <TableCell className="text-right"><Skeleton className="ml-auto h-4 w-28" /></TableCell>
                </TableRow>
              ))
            ) : listQuery.data?.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={5} className="p-0">
                  {status !== "all" ? (
                    <EmptyState
                      icon={ListFilter}
                      title={`Nenhuma conta a receber com status "${RECEIVABLE_STATUS_LABELS[status]}"`}
                      description="Tente outro filtro de status."
                      action={
                        <Button variant="outline" size="sm" onClick={() => { setStatus("all"); setPage(1); }}>
                          Limpar filtro
                        </Button>
                      }
                    />
                  ) : (
                    <EmptyState
                      icon={Inbox}
                      title="Nenhuma conta a receber registrada"
                      description="Contas a receber sao geradas automaticamente quando um agendamento e concluido."
                    />
                  )}
                </TableCell>
              </TableRow>
            ) : (
              listQuery.data?.items.map((item) => (
              <TableRow key={item.id}>
                <TableCell className="font-medium">{item.description}</TableCell>
                <TableCell>{formatDate(item.dueDate)}</TableCell>
                <TableCell className="text-right tabular-nums">{formatCurrency(item.amount)}</TableCell>
                <TableCell>
                  <Badge variant={RECEIVABLE_STATUS_VARIANTS[item.status]}>{RECEIVABLE_STATUS_LABELS[item.status]}</Badge>
                </TableCell>
                <TableCell className="text-right">
                  {item.status === "Pending" && (
                    <Button variant="ghost" size="sm" disabled={receiveMutation.isPending} onClick={() => setConfirmingReceipt(item)}>
                      Confirmar recebimento
                    </Button>
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

      <AlertDialog open={confirmingReceipt !== null} onOpenChange={(open) => !open && setConfirmingReceipt(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Confirmar recebimento?</AlertDialogTitle>
            <AlertDialogDescription>
              {confirmingReceipt &&
                `Marcar "${confirmingReceipt.description}" (${formatCurrency(confirmingReceipt.amount)}) como recebido. Essa acao nao pode ser desfeita.`}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                if (confirmingReceipt) {
                  receiveMutation.mutate(confirmingReceipt.id);
                  setConfirmingReceipt(null);
                }
              }}
            >
              Confirmar recebimento
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Card>
  );
}

function ContasAPagarTab({ accessToken }: { accessToken: string }) {
  const queryClient = useQueryClient();
  const [page, setPage] = React.useState(1);
  const [status, setStatus] = React.useState<AccountPayableStatus | "all">("all");
  const [category, setCategory] = React.useState<ExpenseCategory | "all">("all");
  const [dialogOpen, setDialogOpen] = React.useState(false);
  const [pendingAction, setPendingAction] = React.useState<{ item: AccountPayableSummary; kind: "pay" | "cancel" } | null>(null);

  const listQuery = useQuery({
    queryKey: ["financeiro", "contas-a-pagar", { page, status, category }],
    queryFn: () =>
      listAccountsPayable(
        { page, pageSize: PAGE_SIZE, status: status === "all" ? undefined : status, category: category === "all" ? undefined : category },
        accessToken
      ),
    placeholderData: (previous) => previous,
  });

  const form = useForm<ExpenseFormInput, unknown, ExpenseFormValues>({
    resolver: zodResolver(expenseSchema),
    defaultValues: emptyExpenseForm,
  });

  const invalidateAll = () => {
    queryClient.invalidateQueries({ queryKey: ["financeiro", "contas-a-pagar"] });
    queryClient.invalidateQueries({ queryKey: ["financeiro", "fluxo-de-caixa"] });
  };

  const createMutation = useMutation({
    mutationFn: (values: ExpenseFormValues) => createAccountPayable(values, accessToken),
    onSuccess: () => {
      toast.success("Despesa lancada.");
      invalidateAll();
      setDialogOpen(false);
      form.reset(emptyExpenseForm);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel lancar a despesa."),
  });

  const payMutation = useMutation({
    mutationFn: (id: string) => markAccountPayablePaid(id, accessToken),
    onSuccess: () => {
      toast.success("Pagamento confirmado.");
      invalidateAll();
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel confirmar o pagamento."),
  });

  const cancelMutation = useMutation({
    mutationFn: (id: string) => cancelAccountPayable(id, accessToken),
    onSuccess: () => {
      toast.success("Conta cancelada.");
      invalidateAll();
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel cancelar a conta."),
  });

  const totalPages = Math.max(1, Math.ceil((listQuery.data?.totalCount ?? 0) / PAGE_SIZE));

  return (
    <>
    <Card>
      <CardContent className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex gap-2">
          <Select value={status} onValueChange={(value) => { setStatus(value as AccountPayableStatus | "all"); setPage(1); }}>
            <SelectTrigger className="w-40">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Todos os status</SelectItem>
              {Object.entries(PAYABLE_STATUS_LABELS).map(([value, label]) => (
                <SelectItem key={value} value={value}>
                  {label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select value={category} onValueChange={(value) => { setCategory(value as ExpenseCategory | "all"); setPage(1); }}>
            <SelectTrigger className="w-40">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Todas as categorias</SelectItem>
              {EXPENSE_CATEGORIES.map((value) => (
                <SelectItem key={value} value={value}>
                  {CATEGORY_LABELS[value]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <Button
          onClick={() => {
            form.reset(emptyExpenseForm);
            setDialogOpen(true);
          }}
        >
          <Plus className="size-4" />
          Nova despesa
        </Button>
      </div>

        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Descricao</TableHead>
              <TableHead>Categoria</TableHead>
              <TableHead>Vencimento</TableHead>
              <TableHead className="text-right">Valor</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Acoes</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {listQuery.isLoading ? (
              Array.from({ length: 5 }).map((_, index) => (
                <TableRow key={index}>
                  <TableCell><Skeleton className="h-4 w-40" /></TableCell>
                  <TableCell><Skeleton className="h-4 w-24" /></TableCell>
                  <TableCell><Skeleton className="h-4 w-24" /></TableCell>
                  <TableCell className="text-right"><Skeleton className="ml-auto h-4 w-24" /></TableCell>
                  <TableCell><Skeleton className="h-5 w-20 rounded-full" /></TableCell>
                  <TableCell className="text-right"><Skeleton className="ml-auto h-4 w-28" /></TableCell>
                </TableRow>
              ))
            ) : listQuery.data?.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} className="p-0">
                  {status !== "all" || category !== "all" ? (
                    <EmptyState
                      icon={ListFilter}
                      title="Nenhuma conta a pagar encontrada para este filtro"
                      description="Tente ajustar o status ou a categoria."
                      action={
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => { setStatus("all"); setCategory("all"); setPage(1); }}
                        >
                          Limpar filtros
                        </Button>
                      }
                    />
                  ) : (
                    <EmptyState
                      icon={Receipt}
                      title="Nenhuma conta a pagar registrada"
                      description="Lance sua primeira despesa para comecar a acompanhar as saidas."
                      action={
                        <Button size="sm" onClick={() => { form.reset(emptyExpenseForm); setDialogOpen(true); }}>
                          <Plus className="size-4" />
                          Nova despesa
                        </Button>
                      }
                    />
                  )}
                </TableCell>
              </TableRow>
            ) : (
              listQuery.data?.items.map((item) => (
              <TableRow key={item.id}>
                <TableCell className="font-medium">{item.description}</TableCell>
                <TableCell>{CATEGORY_LABELS[item.category]}</TableCell>
                <TableCell>{formatDate(item.dueDate)}</TableCell>
                <TableCell className="text-right tabular-nums">{formatCurrency(item.amount)}</TableCell>
                <TableCell>
                  <Badge variant={PAYABLE_STATUS_VARIANTS[item.status]}>{PAYABLE_STATUS_LABELS[item.status]}</Badge>
                </TableCell>
                <TableCell className="text-right">
                  {item.status === "Pending" && (
                    <div className="flex justify-end gap-2">
                      <Button
                        variant="ghost"
                        size="sm"
                        disabled={payMutation.isPending}
                        onClick={() => setPendingAction({ item, kind: "pay" })}
                      >
                        Marcar como pago
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        disabled={cancelMutation.isPending}
                        onClick={() => setPendingAction({ item, kind: "cancel" })}
                      >
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

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Nova despesa</DialogTitle>
          </DialogHeader>
          <Form {...form}>
            <form onSubmit={form.handleSubmit((values) => createMutation.mutate(values))} className="flex flex-col gap-3">
              <FormField
                control={form.control}
                name="description"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Descricao</FormLabel>
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
                  name="amount"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Valor</FormLabel>
                      <FormControl>
                        <Input type="number" min={0.01} step={0.01} {...field} value={field.value as number} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="dueDate"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Vencimento</FormLabel>
                      <FormControl>
                        <Input type="date" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>
              <FormField
                control={form.control}
                name="category"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Categoria</FormLabel>
                    <Select value={field.value} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger className="w-full">
                          <SelectValue />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {EXPENSE_CATEGORIES.map((value) => (
                          <SelectItem key={value} value={value}>
                            {CATEGORY_LABELS[value]}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <DialogFooter>
                <Button type="submit" disabled={createMutation.isPending}>
                  {createMutation.isPending ? "Salvando..." : "Salvar"}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      <AlertDialog open={pendingAction !== null} onOpenChange={(open) => !open && setPendingAction(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{pendingAction?.kind === "cancel" ? "Cancelar esta conta?" : "Marcar como pago?"}</AlertDialogTitle>
            <AlertDialogDescription>
              {pendingAction &&
                (pendingAction.kind === "cancel"
                  ? `Cancelar "${pendingAction.item.description}" (${formatCurrency(pendingAction.item.amount)}). Essa acao nao pode ser desfeita.`
                  : `Marcar "${pendingAction.item.description}" (${formatCurrency(pendingAction.item.amount)}) como pago. Essa acao nao pode ser desfeita.`)}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Voltar</AlertDialogCancel>
            <AlertDialogAction
              variant={pendingAction?.kind === "cancel" ? "destructive" : "default"}
              onClick={() => {
                if (!pendingAction) return;
                if (pendingAction.kind === "cancel") {
                  cancelMutation.mutate(pendingAction.item.id);
                } else {
                  payMutation.mutate(pendingAction.item.id);
                }
                setPendingAction(null);
              }}
            >
              {pendingAction?.kind === "cancel" ? "Cancelar conta" : "Marcar como pago"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}

function ComissoesTab({ accessToken }: { accessToken: string }) {
  const queryClient = useQueryClient();
  const [dialogOpen, setDialogOpen] = React.useState(false);
  const [editingResource, setEditingResource] = React.useState<CommissionRuleSummary | null>(null);

  const rulesQuery = useQuery({
    queryKey: ["financeiro", "comissoes"],
    queryFn: () => listCommissionRules(accessToken),
  });

  const form = useForm<CommissionFormInput, unknown, CommissionFormValues>({
    resolver: zodResolver(commissionSchema),
    defaultValues: { calculationType: "Percentage", value: 0 },
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["financeiro", "comissoes"] });

  const upsertMutation = useMutation({
    mutationFn: (values: CommissionFormValues) => {
      if (!editingResource) {
        throw new Error("Nenhum profissional selecionado.");
      }
      return upsertCommissionRule(editingResource.resourceId, values, accessToken);
    },
    onSuccess: () => {
      toast.success("Comissao salva.");
      invalidate();
      setDialogOpen(false);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel salvar a comissao."),
  });

  const removeMutation = useMutation({
    mutationFn: (resourceId: string) => deactivateCommissionRule(resourceId, accessToken),
    onSuccess: () => {
      toast.success("Comissao removida.");
      invalidate();
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel remover a comissao."),
  });

  function openEditDialog(resource: CommissionRuleSummary) {
    setEditingResource(resource);
    form.reset({
      calculationType: resource.calculationType ?? "Percentage",
      value: resource.value ?? 0,
    });
    setDialogOpen(true);
  }

  return (
    <>
    <Card>
      <CardContent className="flex flex-col gap-4">
      <p className="text-muted-foreground text-sm">
        Uma regra por profissional — aplicada a todos os servicos que ele presta.
      </p>

        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Profissional</TableHead>
              <TableHead>Comissao</TableHead>
              <TableHead className="text-right">Acoes</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rulesQuery.isLoading ? (
              Array.from({ length: 5 }).map((_, index) => (
                <TableRow key={index}>
                  <TableCell><Skeleton className="h-4 w-40" /></TableCell>
                  <TableCell><Skeleton className="h-4 w-20" /></TableCell>
                  <TableCell className="text-right"><Skeleton className="ml-auto h-4 w-24" /></TableCell>
                </TableRow>
              ))
            ) : rulesQuery.data?.length === 0 ? (
              <TableRow>
                <TableCell colSpan={3} className="p-0">
                  <EmptyState
                    icon={Users}
                    title="Nenhum profissional cadastrado ainda"
                    description="Cadastre profissionais em Recursos para configurar a comissao de cada um."
                    action={
                      <Button asChild size="sm">
                        <Link href="/recursos">Ir para Recursos</Link>
                      </Button>
                    }
                  />
                </TableCell>
              </TableRow>
            ) : (
              rulesQuery.data?.map((resource) => (
              <TableRow key={resource.resourceId}>
                <TableCell className="font-medium">{resource.resourceName}</TableCell>
                <TableCell>
                  {resource.isActive
                    ? resource.calculationType === "Percentage"
                      ? `${resource.value}%`
                      : formatCurrency(resource.value ?? 0)
                    : "Sem comissao"}
                </TableCell>
                <TableCell className="text-right">
                  <div className="flex justify-end gap-2">
                    <Button variant="ghost" size="sm" onClick={() => openEditDialog(resource)}>
                      {resource.isActive ? "Editar" : "Configurar"}
                    </Button>
                    {resource.isActive && (
                      <Button
                        variant="ghost"
                        size="sm"
                        disabled={removeMutation.isPending}
                        onClick={() => removeMutation.mutate(resource.resourceId)}
                      >
                        Remover
                      </Button>
                    )}
                  </div>
                </TableCell>
              </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </CardContent>
    </Card>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader>
            <DialogTitle>Comissao de {editingResource?.resourceName}</DialogTitle>
          </DialogHeader>
          <Form {...form}>
            <form onSubmit={form.handleSubmit((values) => upsertMutation.mutate(values))} className="flex flex-col gap-3">
              <FormField
                control={form.control}
                name="calculationType"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Tipo</FormLabel>
                    <Select value={field.value} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger className="w-full">
                          <SelectValue />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value="Percentage">Percentual</SelectItem>
                        <SelectItem value="FixedAmount">Valor fixo</SelectItem>
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="value"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Valor</FormLabel>
                    <FormControl>
                      <Input type="number" min={0} step={0.01} {...field} value={field.value as number} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <DialogFooter>
                <Button type="submit" disabled={upsertMutation.isPending}>
                  {upsertMutation.isPending ? "Salvando..." : "Salvar"}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>
    </>
  );
}
