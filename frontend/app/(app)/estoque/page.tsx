"use client";

import * as React from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import {
  AlertTriangle,
  ArrowDownToLine,
  ArrowUpFromLine,
  Boxes,
  CircleDollarSign,
  Inbox,
  Info,
  ListFilter,
  MoreHorizontal,
  Package,
  PackageCheck,
  Plus,
  Search,
  SlidersHorizontal,
} from "lucide-react";

import {
  listProducts,
  getProductById,
  createProduct,
  updateProduct,
  setProductActiveStatus,
  registerStockMovement,
  listStockMovements,
  getInventorySummary,
  ApiError,
  type ProductSummary,
  type StockMovementType,
  type StockMovementReason,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

const PAGE_SIZE = 20;

const TYPE_LABELS: Record<StockMovementType, string> = {
  Entry: "Entrada",
  Exit: "Saída",
};

const REASON_LABELS: Record<StockMovementReason, string> = {
  Purchase: "Compra",
  Sale: "Venda",
  Loss: "Perda",
  Adjustment: "Ajuste",
  Other: "Outro",
};

const REASONS: StockMovementReason[] = ["Purchase", "Sale", "Loss", "Adjustment", "Other"];

function formatCurrency(value: number): string {
  return value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

function formatDateTime(value: string): string {
  return new Date(value).toLocaleString("pt-BR");
}

function toNullable(value: string): string | null {
  return value.trim() === "" ? null : value.trim();
}

function stockLevel(product: ProductSummary): { label: string; percentage: number; barClass: string; badgeClass: string } {
  const target = Math.max(product.minimumStock * 2, 1);
  const percentage = Math.min(100, Math.round((product.quantityInStock / target) * 100));

  if (product.quantityInStock === 0) {
    return { label: "Esgotado", percentage: 0, barClass: "bg-destructive", badgeClass: "text-destructive" };
  }
  if (product.isLowStock) {
    return { label: "Baixo", percentage, barClass: "bg-amber-500", badgeClass: "text-amber-600" };
  }
  return { label: "Saudável", percentage, barClass: "bg-emerald-500", badgeClass: "text-emerald-600" };
}

const productSchema = z.object({
  name: z.string().min(1, "Informe o nome."),
  sku: z.string(),
  category: z.string(),
  description: z.string(),
  quantityInStock: z.coerce.number().int("Use um número inteiro.").min(0, "Não pode ser negativo."),
  minimumStock: z.coerce.number().int("Use um número inteiro.").min(0, "Não pode ser negativo."),
  costPrice: z.string(),
  salePrice: z.string(),
});
type ProductFormInput = z.input<typeof productSchema>;
type ProductFormValues = z.output<typeof productSchema>;
const emptyProductForm: ProductFormInput = {
  name: "",
  sku: "",
  category: "",
  description: "",
  quantityInStock: 0,
  minimumStock: 0,
  costPrice: "",
  salePrice: "",
};

const movementSchema = z.object({
  type: z.enum(["Entry", "Exit"]),
  quantity: z.coerce.number().int("Use um numero inteiro.").positive("Informe uma quantidade maior que zero."),
  reason: z.enum(["Purchase", "Sale", "Loss", "Adjustment", "Other"]),
  notes: z.string(),
});
type MovementFormInput = z.input<typeof movementSchema>;
type MovementFormValues = z.output<typeof movementSchema>;
const emptyMovementForm: MovementFormInput = { type: "Entry", quantity: 1, reason: "Purchase", notes: "" };

export default function EstoquePage() {
  const { session } = useSession();

  if (!session) {
    return null;
  }

  return (
    <div className="flex w-full flex-1 flex-col gap-6">
      <section className="relative overflow-hidden rounded-2xl border bg-gradient-to-br from-primary/12 via-card to-card p-5 sm:p-6">
        <div aria-hidden className="absolute -right-10 -top-16 size-52 rounded-full bg-primary/10 blur-3xl" />
        <div className="relative flex items-start gap-4">
          <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-primary text-primary-foreground shadow-lg shadow-primary/20">
            <Boxes className="size-5" />
          </span>
          <div>
            <p className="text-xs font-semibold tracking-[0.18em] text-primary uppercase">Controle de produtos</p>
            <h2 className="mt-1 text-xl font-semibold tracking-tight">Estoque sob controle, sem surpresas</h2>
            <p className="mt-1 max-w-2xl text-sm leading-relaxed text-muted-foreground">
              Acompanhe saldos, identifique reposições e registre cada entrada ou saída em um só lugar.
            </p>
          </div>
        </div>
      </section>

      <Tabs defaultValue="produtos" className="space-y-4">
        <TabsList className="h-auto w-fit rounded-xl p-1">
          <TabsTrigger value="produtos">Produtos</TabsTrigger>
          <TabsTrigger value="movimentacoes">Movimentações</TabsTrigger>
        </TabsList>

        <TabsContent value="produtos" className="mt-0">
          <ProdutosTab accessToken={session.accessToken} />
        </TabsContent>
        <TabsContent value="movimentacoes" className="mt-0">
          <MovimentacoesTab accessToken={session.accessToken} />
        </TabsContent>
      </Tabs>
    </div>
  );
}

function ProdutosTab({ accessToken }: { accessToken: string }) {
  const queryClient = useQueryClient();
  const [page, setPage] = React.useState(1);
  const [searchInput, setSearchInput] = React.useState("");
  const [search, setSearch] = React.useState("");
  const [lowStockOnly, setLowStockOnly] = React.useState(false);
  const [statusFilter, setStatusFilter] = React.useState<"all" | "active" | "inactive">("all");
  const [dialogOpen, setDialogOpen] = React.useState(false);
  const [editingProduct, setEditingProduct] = React.useState<ProductSummary | null>(null);
  const [movementDialogOpen, setMovementDialogOpen] = React.useState(false);
  const [movementProduct, setMovementProduct] = React.useState<ProductSummary | null>(null);

  const listQuery = useQuery({
    queryKey: ["estoque", "produtos", { page, search, lowStockOnly, statusFilter }],
    queryFn: () => listProducts({
      page,
      pageSize: PAGE_SIZE,
      search: search || undefined,
      lowStockOnly,
      isActive: statusFilter === "all" ? undefined : statusFilter === "active",
    }, accessToken),
    placeholderData: (previous) => previous,
  });

  const summaryQuery = useQuery({
    queryKey: ["estoque", "resumo"],
    queryFn: () => getInventorySummary(accessToken),
  });

  React.useEffect(() => {
    const timer = window.setTimeout(() => {
      setPage(1);
      setSearch(searchInput.trim());
    }, 300);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  const form = useForm<ProductFormInput, unknown, ProductFormValues>({
    resolver: zodResolver(productSchema),
    defaultValues: emptyProductForm,
  });

  const movementForm = useForm<MovementFormInput, unknown, MovementFormValues>({
    resolver: zodResolver(movementSchema),
    defaultValues: emptyMovementForm,
  });

  const invalidateAll = () => {
    queryClient.invalidateQueries({ queryKey: ["estoque", "produtos"] });
    queryClient.invalidateQueries({ queryKey: ["estoque", "movimentacoes"] });
    queryClient.invalidateQueries({ queryKey: ["estoque", "resumo"] });
  };

  const createMutation = useMutation({
    mutationFn: (values: ProductFormValues) =>
      createProduct(
        {
          name: values.name,
          sku: toNullable(values.sku),
          category: toNullable(values.category),
          description: toNullable(values.description),
          quantityInStock: values.quantityInStock,
          minimumStock: values.minimumStock,
          costPrice: values.costPrice.trim() === "" ? null : Number(values.costPrice),
          salePrice: values.salePrice.trim() === "" ? null : Number(values.salePrice),
        },
        accessToken
      ),
    onSuccess: () => {
      toast.success("Produto cadastrado.");
      invalidateAll();
      setDialogOpen(false);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Não foi possível cadastrar o produto."),
  });

  const updateMutation = useMutation({
    mutationFn: (values: ProductFormValues) => {
      if (!editingProduct) {
        throw new Error("Nenhum produto selecionado.");
      }
      return updateProduct(
        editingProduct.id,
        {
          name: values.name,
          sku: toNullable(values.sku),
          category: toNullable(values.category),
          description: toNullable(values.description),
          minimumStock: values.minimumStock,
          costPrice: values.costPrice.trim() === "" ? null : Number(values.costPrice),
          salePrice: values.salePrice.trim() === "" ? null : Number(values.salePrice),
        },
        accessToken
      );
    },
    onSuccess: () => {
      toast.success("Produto atualizado.");
      invalidateAll();
      setDialogOpen(false);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Não foi possível atualizar o produto."),
  });

  const statusMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setProductActiveStatus(id, isActive, accessToken),
    onSuccess: invalidateAll,
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Não foi possível atualizar o status."),
  });

  const movementMutation = useMutation({
    mutationFn: (values: MovementFormValues) => {
      if (!movementProduct) {
        throw new Error("Nenhum produto selecionado.");
      }
      return registerStockMovement(
        movementProduct.id,
        { type: values.type, quantity: values.quantity, reason: values.reason, notes: toNullable(values.notes) },
        accessToken
      );
    },
    onSuccess: () => {
      toast.success("Movimentação registrada.");
      invalidateAll();
      setMovementDialogOpen(false);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Não foi possível registrar a movimentação."),
  });

  const totalPages = Math.max(1, Math.ceil((listQuery.data?.totalCount ?? 0) / PAGE_SIZE));
  const summary = summaryQuery.data;
  const totalStockValue = summary?.totalStockValue.reduce((total, point) => total + point.total, 0) ?? 0;
  const healthyProductCount = summary
    ? Math.max(0, summary.activeProductCount - summary.lowStockCount)
    : 0;
  const totalUnits = summary?.totalUnitsInStock
    ?? listQuery.data?.items.reduce((total, product) => total + product.quantityInStock, 0)
    ?? 0;
  const outOfStockCount = summary?.outOfStockCount
    ?? listQuery.data?.items.filter((product) => product.quantityInStock === 0).length
    ?? 0;
  const hasProductFilters = Boolean(search || lowStockOnly || statusFilter !== "all");

  function openCreateDialog() {
    setEditingProduct(null);
    form.reset(emptyProductForm);
    setDialogOpen(true);
  }

  async function openEditDialog(product: ProductSummary) {
    try {
      const details = await getProductById(product.id, accessToken);
      setEditingProduct(product);
      form.reset({
        name: details.name,
        sku: details.sku ?? "",
        category: details.category ?? "",
        description: details.description ?? "",
        quantityInStock: details.quantityInStock,
        minimumStock: details.minimumStock,
        costPrice: details.costPrice !== null ? String(details.costPrice) : "",
        salePrice: details.salePrice !== null ? String(details.salePrice) : "",
      });
      setDialogOpen(true);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Não foi possível carregar o produto.");
    }
  }

  function openMovementDialog(product: ProductSummary) {
    setMovementProduct(product);
    movementForm.reset(emptyMovementForm);
    setMovementDialogOpen(true);
  }

  return (
    <div className="flex flex-col gap-5">
      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4" aria-label="Resumo do estoque">
        {[
          { label: "Produtos ativos", value: summary?.activeProductCount ?? 0, hint: `${healthyProductCount} com saldo saudável`, icon: PackageCheck, color: "text-primary bg-primary/10" },
          { label: "Unidades em estoque", value: totalUnits, hint: "Somando todos os produtos", icon: Boxes, color: "text-sky-600 bg-sky-500/10" },
          { label: "Valor para venda", value: formatCurrency(totalStockValue), hint: "Valor potencial do estoque", icon: CircleDollarSign, color: "text-emerald-600 bg-emerald-500/10" },
          { label: "Precisam de atenção", value: summary?.lowStockCount ?? 0, hint: `${outOfStockCount} sem estoque`, icon: AlertTriangle, color: "text-amber-600 bg-amber-500/10" },
        ].map((metric) => {
          const Icon = metric.icon;
          return (
            <Card key={metric.label}>
              <CardContent className="flex items-center gap-3 p-4">
                <span className={`flex size-10 shrink-0 items-center justify-center rounded-xl ${metric.color}`}><Icon className="size-5" /></span>
                <div className="min-w-0">
                  {summaryQuery.isLoading ? <Skeleton className="mb-1 h-7 w-20" /> : <p className="truncate text-2xl font-semibold tracking-tight tabular-nums">{metric.value}</p>}
                  <p className="text-xs font-medium text-muted-foreground">{metric.label}</p>
                  <p className="mt-0.5 truncate text-[11px] text-muted-foreground/75">{metric.hint}</p>
                </div>
              </CardContent>
            </Card>
          );
        })}
      </section>

      {(summary?.lowStockCount ?? 0) > 0 && (
        <section className="flex flex-col justify-between gap-3 rounded-xl border border-amber-500/25 bg-amber-500/8 p-4 sm:flex-row sm:items-center" role="status">
          <div className="flex items-start gap-3">
            <span className="mt-0.5 flex size-9 shrink-0 items-center justify-center rounded-lg bg-amber-500/15 text-amber-600"><AlertTriangle className="size-4" /></span>
            <div><p className="text-sm font-semibold">Reposição recomendada</p><p className="text-xs text-muted-foreground">{summary?.lowStockCount} {summary?.lowStockCount === 1 ? "produto atingiu" : "produtos atingiram"} o estoque mínimo.</p></div>
          </div>
          <Button type="button" size="sm" variant="outline" onClick={() => { setLowStockOnly(true); setStatusFilter("active"); setPage(1); }}>Ver produtos</Button>
        </section>
      )}

      <Card className="overflow-hidden">
        <CardContent className="flex flex-col gap-4 p-0">
          <div className="flex flex-col justify-between gap-3 border-b p-4 sm:flex-row sm:items-center sm:p-5">
            <div><h3 className="font-semibold">Produtos cadastrados</h3><p className="text-xs text-muted-foreground">Consulte saldos, preços e faça movimentações rápidas.</p></div>
            <Button onClick={openCreateDialog}><Plus className="size-4" />Novo produto</Button>
          </div>

          <div className="flex flex-col gap-3 px-4 sm:flex-row sm:items-center sm:px-5">
            <div className="relative min-w-0 flex-1 sm:max-w-sm">
              <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input placeholder="Buscar por nome, SKU ou categoria" value={searchInput} onChange={(event) => setSearchInput(event.target.value)} className="pl-9" aria-label="Buscar produtos" />
            </div>
            <Select value={statusFilter} onValueChange={(value) => { setStatusFilter(value as typeof statusFilter); setPage(1); }}>
              <SelectTrigger className="w-full sm:w-40" aria-label="Filtrar por status"><SlidersHorizontal className="size-4" /><SelectValue /></SelectTrigger>
              <SelectContent><SelectItem value="all">Todos os status</SelectItem><SelectItem value="active">Ativos</SelectItem><SelectItem value="inactive">Inativos</SelectItem></SelectContent>
            </Select>
            <Button type="button" variant={lowStockOnly ? "secondary" : "outline"} aria-pressed={lowStockOnly} onClick={() => { setPage(1); setLowStockOnly((current) => !current); }}>
              <AlertTriangle className="size-4" />Estoque baixo
            </Button>
            {hasProductFilters && <Button type="button" variant="ghost" onClick={() => { setSearchInput(""); setSearch(""); setLowStockOnly(false); setStatusFilter("all"); setPage(1); }}>Limpar</Button>}
          </div>

          <div className="overflow-x-auto px-4 pb-1 sm:px-5">
          <Table className="min-w-[860px]">
            <TableHeader>
              <TableRow>
                <TableHead>Nome</TableHead>
                <TableHead>SKU</TableHead>
                <TableHead>Categoria</TableHead>
                <TableHead>Saldo atual</TableHead>
                <TableHead className="text-right">Venda / margem</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="text-right">Ações</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {listQuery.isLoading ? (
                Array.from({ length: 5 }).map((_, index) => (
                  <TableRow key={index}>
                    <TableCell><Skeleton className="h-4 w-32" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-16" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-20" /></TableCell>
                    <TableCell className="text-right"><Skeleton className="ml-auto h-4 w-12" /></TableCell>
                    <TableCell className="text-right"><Skeleton className="ml-auto h-4 w-20" /></TableCell>
                    <TableCell><Skeleton className="h-5 w-14 rounded-full" /></TableCell>
                    <TableCell className="text-right"><Skeleton className="ml-auto h-4 w-32" /></TableCell>
                  </TableRow>
                ))
              ) : listQuery.data?.items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={7} className="p-0">
                    {hasProductFilters ? (
                      <EmptyState
                        icon={ListFilter}
                        title="Nenhum produto encontrado para este filtro"
                        description="Tente ajustar a busca ou o filtro de estoque baixo."
                        action={
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => {
                              setSearchInput("");
                              setSearch("");
                              setLowStockOnly(false);
                              setStatusFilter("all");
                              setPage(1);
                            }}
                          >
                            Limpar filtros
                          </Button>
                        }
                      />
                    ) : (
                      <EmptyState
                        icon={Package}
                        title="Nenhum produto cadastrado ainda"
                        description="Cadastre os produtos revendidos pelo seu estabelecimento."
                        action={
                          <Button size="sm" onClick={openCreateDialog}>
                            <Plus className="size-4" />
                            Novo produto
                          </Button>
                        }
                      />
                    )}
                  </TableCell>
                </TableRow>
              ) : (
                listQuery.data?.items.map((product) => {
                  const level = stockLevel(product);
                  const margin = product.costPrice !== null && product.salePrice !== null && product.costPrice > 0
                    ? Math.round(((product.salePrice - product.costPrice) / product.costPrice) * 100)
                    : null;
                  return (
                  <TableRow key={product.id} className="group">
                    <TableCell><div className="flex items-center gap-3"><span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary"><Package className="size-4" /></span><span className="font-medium">{product.name}</span></div></TableCell>
                    <TableCell className="font-mono text-xs text-muted-foreground">{product.sku ?? "—"}</TableCell>
                    <TableCell><Badge variant="outline" className="font-normal">{product.category ?? "Sem categoria"}</Badge></TableCell>
                    <TableCell>
                      <div className="min-w-32 space-y-1.5">
                        <div className="flex items-center justify-between gap-3 text-xs"><span className="font-semibold tabular-nums">{product.quantityInStock} un.</span><span className={level.badgeClass}>{level.label}</span></div>
                        <div className="h-1.5 overflow-hidden rounded-full bg-muted" role="progressbar" aria-label={`Nível de estoque de ${product.name}`} aria-valuenow={level.percentage} aria-valuemin={0} aria-valuemax={100}><div className={`h-full rounded-full transition-[width] ${level.barClass}`} style={{ width: `${level.percentage}%` }} /></div>
                        <p className="text-[10px] text-muted-foreground">Mínimo: {product.minimumStock}</p>
                      </div>
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      <p className="font-medium">{product.salePrice !== null ? formatCurrency(product.salePrice) : "—"}</p>
                      {margin !== null && <p className={`text-[10px] ${margin >= 0 ? "text-emerald-600" : "text-destructive"}`}>{margin}% sobre o custo</p>}
                    </TableCell>
                    <TableCell>
                      <Badge variant={product.isActive ? "default" : "outline"}>{product.isActive ? "Ativo" : "Inativo"}</Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex items-center justify-end gap-1">
                        <Button variant="outline" size="sm" onClick={() => openMovementDialog(product)}>
                          <ArrowDownToLine className="size-3.5" />Movimentar
                        </Button>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="icon" aria-label={`Mais ações de ${product.name}`}>
                              <MoreHorizontal className="size-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end" className="w-40">
                            <DropdownMenuItem onSelect={() => openEditDialog(product)}>Editar</DropdownMenuItem>
                            <DropdownMenuItem
                              variant={product.isActive ? "destructive" : "default"}
                              onSelect={() => statusMutation.mutate({ id: product.id, isActive: !product.isActive })}
                            >
                              {product.isActive ? "Desativar" : "Ativar"}
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </div>
                    </TableCell>
                  </TableRow>
                  );
                })
              )}
            </TableBody>
          </Table>
          </div>

          <div className="flex items-center justify-between border-t px-4 pb-4 pt-3 text-sm sm:px-5">
            <span className="text-muted-foreground" aria-live="polite">
              Página {listQuery.data?.page ?? page} de {totalPages} · {listQuery.data?.totalCount ?? 0} produtos
            </span>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((current) => current - 1)}>
                Anterior
              </Button>
              <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage((current) => current + 1)}>
                Próxima
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{editingProduct ? "Editar produto" : "Novo produto"}</DialogTitle>
          </DialogHeader>
          <Form {...form}>
            <form
              onSubmit={form.handleSubmit((values) =>
                editingProduct ? updateMutation.mutate(values) : createMutation.mutate(values)
              )}
              className="flex flex-col gap-3"
            >
              <FormField
                control={form.control}
                name="name"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Nome</FormLabel>
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
                  name="sku"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel className="gap-1">
                        SKU
                        <Tooltip>
                          <TooltipTrigger type="button" className="text-muted-foreground hover:text-foreground">
                            <Info className="size-3.5" aria-hidden />
                            <span className="sr-only">O que e SKU?</span>
                          </TooltipTrigger>
                          <TooltipContent>
                            Código único usado para identificar este produto no seu estoque (ex.: SHP-001).
                          </TooltipContent>
                        </Tooltip>
                      </FormLabel>
                      <FormControl>
                        <Input {...field} />
                      </FormControl>
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="category"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Categoria</FormLabel>
                      <FormControl>
                        <Input {...field} />
                      </FormControl>
                    </FormItem>
                  )}
                />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <FormField
                  control={form.control}
                  name="costPrice"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Preço de custo</FormLabel>
                      <FormControl>
                        <Input type="number" min={0} step={0.01} {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="salePrice"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Preço de venda</FormLabel>
                      <FormControl>
                        <Input type="number" min={0} step={0.01} {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>
              <div className="grid grid-cols-2 gap-3">
                {!editingProduct && (
                  <FormField
                    control={form.control}
                    name="quantityInStock"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Quantidade inicial</FormLabel>
                        <FormControl>
                          <Input type="number" min={0} {...field} value={field.value as number} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                )}
                <FormField
                  control={form.control}
                  name="minimumStock"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Estoque minimo</FormLabel>
                      <FormControl>
                        <Input type="number" min={0} {...field} value={field.value as number} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>
              <FormField
                control={form.control}
                name="description"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Descricao</FormLabel>
                    <FormControl>
                      <Textarea rows={3} {...field} />
                    </FormControl>
                  </FormItem>
                )}
              />
              <DialogFooter>
                <Button type="submit" disabled={createMutation.isPending || updateMutation.isPending}>
                  {createMutation.isPending || updateMutation.isPending ? "Salvando..." : "Salvar"}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      <Dialog open={movementDialogOpen} onOpenChange={setMovementDialogOpen}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader>
            <DialogTitle>Movimentar {movementProduct?.name}</DialogTitle>
          </DialogHeader>
          <Form {...movementForm}>
            <form onSubmit={movementForm.handleSubmit((values) => movementMutation.mutate(values))} className="flex flex-col gap-3">
              <FormField
                control={movementForm.control}
                name="type"
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
                        <SelectItem value="Entry">Entrada</SelectItem>
                        <SelectItem value="Exit">Saída</SelectItem>
                      </SelectContent>
                    </Select>
                  </FormItem>
                )}
              />
              <FormField
                control={movementForm.control}
                name="quantity"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Quantidade</FormLabel>
                    <FormControl>
                      <Input type="number" min={1} {...field} value={field.value as number} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={movementForm.control}
                name="reason"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Motivo</FormLabel>
                    <Select value={field.value} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger className="w-full">
                          <SelectValue />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {REASONS.map((reason) => (
                          <SelectItem key={reason} value={reason}>
                            {REASON_LABELS[reason]}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </FormItem>
                )}
              />
              <FormField
                control={movementForm.control}
                name="notes"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Observações (opcional)</FormLabel>
                    <FormControl>
                      <Textarea rows={2} {...field} />
                    </FormControl>
                  </FormItem>
                )}
              />
              <DialogFooter>
                <Button type="submit" disabled={movementMutation.isPending}>
                  {movementMutation.isPending ? "Salvando..." : "Registrar"}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function MovimentacoesTab({ accessToken }: { accessToken: string }) {
  const [page, setPage] = React.useState(1);
  const [productId, setProductId] = React.useState<string>("all");
  const [type, setType] = React.useState<StockMovementType | "all">("all");
  const [reason, setReason] = React.useState<StockMovementReason | "all">("all");
  const [from, setFrom] = React.useState("");
  const [to, setTo] = React.useState("");

  const productsQuery = useQuery({
    queryKey: ["estoque", "produtos", "all-for-filter"],
    queryFn: () => listProducts({ page: 1, pageSize: 100 }, accessToken),
  });

  const movementsQuery = useQuery({
    queryKey: ["estoque", "movimentacoes", { page, productId, type, reason, from, to }],
    queryFn: () =>
      listStockMovements(
        {
          page,
          pageSize: PAGE_SIZE,
          productId: productId === "all" ? undefined : productId,
          type: type === "all" ? undefined : type,
          reason: reason === "all" ? undefined : reason,
          from: from || undefined,
          to: to || undefined,
        },
        accessToken
      ),
    placeholderData: (previous) => previous,
  });

  const totalPages = Math.max(1, Math.ceil((movementsQuery.data?.totalCount ?? 0) / PAGE_SIZE));

  const hasFilter = productId !== "all" || type !== "all" || reason !== "all" || Boolean(from || to);

  return (
    <div className="flex flex-col gap-4">
      <Card className="overflow-hidden">
        <CardContent className="flex flex-col gap-4 p-0">
          <div className="flex flex-col justify-between gap-2 border-b p-4 sm:flex-row sm:items-center sm:p-5">
            <div><h3 className="font-semibold">Histórico de movimentações</h3><p className="text-xs text-muted-foreground">Rastreie cada entrada, saída e ajuste realizado no estoque.</p></div>
            <Badge variant="secondary">{movementsQuery.data?.totalCount ?? 0} registros</Badge>
          </div>

          <div className="grid gap-2 px-4 sm:grid-cols-2 lg:grid-cols-5 sm:px-5" aria-label="Filtros de movimentações">
            <Select value={productId} onValueChange={(value) => { setProductId(value); setPage(1); }}>
              <SelectTrigger className="w-full" aria-label="Filtrar por produto">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Todos os produtos</SelectItem>
                {productsQuery.data?.items.map((product) => (
                  <SelectItem key={product.id} value={product.id}>
                    {product.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select value={type} onValueChange={(value) => { setType(value as StockMovementType | "all"); setPage(1); }}>
              <SelectTrigger className="w-full" aria-label="Filtrar por tipo">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Todos os tipos</SelectItem>
                <SelectItem value="Entry">Entrada</SelectItem>
                <SelectItem value="Exit">Saída</SelectItem>
              </SelectContent>
            </Select>
            <Select value={reason} onValueChange={(value) => { setReason(value as StockMovementReason | "all"); setPage(1); }}>
              <SelectTrigger className="w-full" aria-label="Filtrar por motivo">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Todos os motivos</SelectItem>
                {REASONS.map((reasonOption) => (
                  <SelectItem key={reasonOption} value={reasonOption}>
                    {REASON_LABELS[reasonOption]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Input type="date" value={from} max={to || undefined} onChange={(event) => { setFrom(event.target.value); setPage(1); }} aria-label="Data inicial" />
            <Input type="date" value={to} min={from || undefined} onChange={(event) => { setTo(event.target.value); setPage(1); }} aria-label="Data final" />
          </div>

          {hasFilter && <div className="px-4 sm:px-5"><Button variant="ghost" size="sm" onClick={() => { setProductId("all"); setType("all"); setReason("all"); setFrom(""); setTo(""); setPage(1); }}>Limpar filtros</Button></div>}

          <div className="overflow-x-auto px-4 sm:px-5"><Table className="min-w-[760px]">
            <TableHeader>
              <TableRow>
                <TableHead>Data</TableHead>
                <TableHead>Produto</TableHead>
                <TableHead>Tipo</TableHead>
                <TableHead className="text-right">Quantidade</TableHead>
                <TableHead>Motivo</TableHead>
                <TableHead>Observações</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {movementsQuery.isLoading ? (
                Array.from({ length: 5 }).map((_, index) => (
                  <TableRow key={index}>
                    <TableCell><Skeleton className="h-4 w-32" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-28" /></TableCell>
                    <TableCell><Skeleton className="h-5 w-16 rounded-full" /></TableCell>
                    <TableCell className="text-right"><Skeleton className="ml-auto h-4 w-12" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-20" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-24" /></TableCell>
                  </TableRow>
                ))
              ) : movementsQuery.data?.items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="p-0">
                    <EmptyState
                      icon={hasFilter ? ListFilter : Inbox}
                      title={hasFilter ? "Nenhuma movimentação encontrada para este filtro" : "Nenhuma movimentação registrada ainda"}
                      description={
                        hasFilter
                          ? "Tente ajustar o produto, tipo ou motivo selecionado."
                          : "As movimentações aparecem aqui quando você registra a entrada ou saída de um produto."
                      }
                      action={
                        hasFilter ? (
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => {
                              setProductId("all");
                              setType("all");
                              setReason("all");
                              setFrom("");
                              setTo("");
                              setPage(1);
                            }}
                          >
                            Limpar filtros
                          </Button>
                        ) : undefined
                      }
                    />
                  </TableCell>
                </TableRow>
              ) : (
                movementsQuery.data?.items.map((movement) => (
                  <TableRow key={movement.id}>
                    <TableCell className="text-xs text-muted-foreground">{formatDateTime(movement.occurredAtUtc)}</TableCell>
                    <TableCell><div className="flex items-center gap-2.5"><span className={`flex size-8 shrink-0 items-center justify-center rounded-lg ${movement.type === "Entry" ? "bg-emerald-500/10 text-emerald-600" : "bg-rose-500/10 text-rose-600"}`}>{movement.type === "Entry" ? <ArrowDownToLine className="size-4" /> : <ArrowUpFromLine className="size-4" />}</span><span className="font-medium">{movement.productName}</span></div></TableCell>
                    <TableCell>
                      <Badge variant={movement.type === "Entry" ? "success" : "destructive"}>{TYPE_LABELS[movement.type]}</Badge>
                    </TableCell>
                    <TableCell className={`text-right font-semibold tabular-nums ${movement.type === "Entry" ? "text-emerald-600" : "text-rose-600"}`}>{movement.type === "Entry" ? "+" : "−"}{movement.quantity}</TableCell>
                    <TableCell>{REASON_LABELS[movement.reason]}</TableCell>
                    <TableCell className="text-muted-foreground">{movement.notes ?? "—"}</TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table></div>

          <div className="flex items-center justify-between border-t px-4 pb-4 pt-3 text-sm sm:px-5">
            <span className="text-muted-foreground" aria-live="polite">
              Página {movementsQuery.data?.page ?? page} de {totalPages}
            </span>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((current) => current - 1)}>
                Anterior
              </Button>
              <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage((current) => current + 1)}>
                Próxima
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
