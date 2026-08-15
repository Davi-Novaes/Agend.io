"use client";

import * as React from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Inbox, ListFilter, Package, Plus } from "lucide-react";

import {
  listProducts,
  getProductById,
  createProduct,
  updateProduct,
  setProductActiveStatus,
  registerStockMovement,
  listStockMovements,
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

const PAGE_SIZE = 20;

const TYPE_LABELS: Record<StockMovementType, string> = {
  Entry: "Entrada",
  Exit: "Saida",
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

const productSchema = z.object({
  name: z.string().min(1, "Informe o nome."),
  sku: z.string(),
  category: z.string(),
  description: z.string(),
  quantityInStock: z.coerce.number().int("Use um numero inteiro.").min(0, "Nao pode ser negativo."),
  minimumStock: z.coerce.number().int("Use um numero inteiro.").min(0, "Nao pode ser negativo."),
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
    <div className="mx-auto flex w-full max-w-5xl flex-1 flex-col">
      <div className="mb-6">
        <p className="text-muted-foreground text-sm">Produtos revendidos e movimentacoes de entrada e saida.</p>
      </div>

      <Tabs defaultValue="produtos">
        <TabsList>
          <TabsTrigger value="produtos">Produtos</TabsTrigger>
          <TabsTrigger value="movimentacoes">Movimentacoes</TabsTrigger>
        </TabsList>

        <TabsContent value="produtos" className="mt-4">
          <ProdutosTab accessToken={session.accessToken} />
        </TabsContent>
        <TabsContent value="movimentacoes" className="mt-4">
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
  const [dialogOpen, setDialogOpen] = React.useState(false);
  const [editingProduct, setEditingProduct] = React.useState<ProductSummary | null>(null);
  const [movementDialogOpen, setMovementDialogOpen] = React.useState(false);
  const [movementProduct, setMovementProduct] = React.useState<ProductSummary | null>(null);

  const listQuery = useQuery({
    queryKey: ["estoque", "produtos", { page, search, lowStockOnly }],
    queryFn: () => listProducts({ page, pageSize: PAGE_SIZE, search: search || undefined, lowStockOnly }, accessToken),
    placeholderData: (previous) => previous,
  });

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
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel cadastrar o produto."),
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
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel atualizar o produto."),
  });

  const statusMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setProductActiveStatus(id, isActive, accessToken),
    onSuccess: invalidateAll,
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel atualizar o status."),
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
      toast.success("Movimentacao registrada.");
      invalidateAll();
      setMovementDialogOpen(false);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel registrar a movimentacao."),
  });

  const totalPages = Math.max(1, Math.ceil((listQuery.data?.totalCount ?? 0) / PAGE_SIZE));

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
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel carregar o produto.");
    }
  }

  function openMovementDialog(product: ProductSummary) {
    setMovementProduct(product);
    movementForm.reset(emptyMovementForm);
    setMovementDialogOpen(true);
  }

  return (
    <div className="flex flex-col gap-4">
      <Card>
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="flex flex-wrap items-center gap-2">
              <Input
                placeholder="Buscar por nome..."
                value={searchInput}
                onChange={(event) => setSearchInput(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === "Enter") {
                    setPage(1);
                    setSearch(searchInput);
                  }
                }}
                className="w-56"
              />
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => {
                  setPage(1);
                  setSearch(searchInput);
                }}
              >
                Buscar
              </Button>
              <Button
                type="button"
                variant={lowStockOnly ? "secondary" : "outline"}
                size="sm"
                aria-pressed={lowStockOnly}
                onClick={() => {
                  setPage(1);
                  setLowStockOnly((current) => !current);
                }}
              >
                Estoque baixo
              </Button>
            </div>
            <Button onClick={openCreateDialog}>
              <Plus className="size-4" />
              Novo produto
            </Button>
          </div>

          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Nome</TableHead>
                <TableHead>SKU</TableHead>
                <TableHead>Categoria</TableHead>
                <TableHead className="text-right">Quantidade</TableHead>
                <TableHead className="text-right">Preco</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="text-right">Acoes</TableHead>
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
                    {search || lowStockOnly ? (
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
                listQuery.data?.items.map((product) => (
                  <TableRow key={product.id}>
                    <TableCell className="font-medium">{product.name}</TableCell>
                    <TableCell>{product.sku ?? "—"}</TableCell>
                    <TableCell>{product.category ?? "—"}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      <div className="flex items-center justify-end gap-2">
                        {product.isLowStock && (
                          <Badge variant="destructive" title={`Estoque minimo: ${product.minimumStock}`}>
                            Baixo
                          </Badge>
                        )}
                        {product.quantityInStock}
                      </div>
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {product.salePrice !== null ? formatCurrency(product.salePrice) : "—"}
                    </TableCell>
                    <TableCell>
                      <Badge variant={product.isActive ? "default" : "outline"}>{product.isActive ? "Ativo" : "Inativo"}</Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-2">
                        <Button variant="ghost" size="sm" onClick={() => openMovementDialog(product)}>
                          Movimentar
                        </Button>
                        <Button variant="ghost" size="sm" onClick={() => openEditDialog(product)}>
                          Editar
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => statusMutation.mutate({ id: product.id, isActive: !product.isActive })}
                        >
                          {product.isActive ? "Desativar" : "Ativar"}
                        </Button>
                      </div>
                    </TableCell>
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
                      <FormLabel>SKU</FormLabel>
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
                      <FormLabel>Preco de custo</FormLabel>
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
                      <FormLabel>Preco de venda</FormLabel>
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
                        <SelectItem value="Exit">Saida</SelectItem>
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
                    <FormLabel>Observacoes (opcional)</FormLabel>
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

  const productsQuery = useQuery({
    queryKey: ["estoque", "produtos", "all-for-filter"],
    queryFn: () => listProducts({ page: 1, pageSize: 100 }, accessToken),
  });

  const movementsQuery = useQuery({
    queryKey: ["estoque", "movimentacoes", { page, productId, type, reason }],
    queryFn: () =>
      listStockMovements(
        {
          page,
          pageSize: PAGE_SIZE,
          productId: productId === "all" ? undefined : productId,
          type: type === "all" ? undefined : type,
          reason: reason === "all" ? undefined : reason,
        },
        accessToken
      ),
    placeholderData: (previous) => previous,
  });

  const totalPages = Math.max(1, Math.ceil((movementsQuery.data?.totalCount ?? 0) / PAGE_SIZE));

  const hasFilter = productId !== "all" || type !== "all" || reason !== "all";

  return (
    <div className="flex flex-col gap-4">
      <Card>
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-wrap gap-2">
            <Select value={productId} onValueChange={(value) => { setProductId(value); setPage(1); }}>
              <SelectTrigger className="w-48">
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
              <SelectTrigger className="w-36">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Todos os tipos</SelectItem>
                <SelectItem value="Entry">Entrada</SelectItem>
                <SelectItem value="Exit">Saida</SelectItem>
              </SelectContent>
            </Select>
            <Select value={reason} onValueChange={(value) => { setReason(value as StockMovementReason | "all"); setPage(1); }}>
              <SelectTrigger className="w-40">
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
          </div>

          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Data</TableHead>
                <TableHead>Produto</TableHead>
                <TableHead>Tipo</TableHead>
                <TableHead className="text-right">Quantidade</TableHead>
                <TableHead>Motivo</TableHead>
                <TableHead>Observacoes</TableHead>
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
                      title={hasFilter ? "Nenhuma movimentacao encontrada para este filtro" : "Nenhuma movimentacao registrada ainda"}
                      description={
                        hasFilter
                          ? "Tente ajustar o produto, tipo ou motivo selecionado."
                          : "Movimentacoes aparecem aqui quando voce registra entrada ou saida de um produto."
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
                    <TableCell>{formatDateTime(movement.occurredAtUtc)}</TableCell>
                    <TableCell className="font-medium">{movement.productName}</TableCell>
                    <TableCell>
                      <Badge variant={movement.type === "Entry" ? "default" : "outline"}>{TYPE_LABELS[movement.type]}</Badge>
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{movement.quantity}</TableCell>
                    <TableCell>{REASON_LABELS[movement.reason]}</TableCell>
                    <TableCell className="text-muted-foreground">{movement.notes ?? "—"}</TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>

          <div className="flex items-center justify-between text-sm">
            <span className="text-muted-foreground">
              Pagina {movementsQuery.data?.page ?? page} de {totalPages}
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
    </div>
  );
}
