"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { ArrowLeft, Plus, Search } from "lucide-react";

import {
  listServices,
  getServiceById,
  createService,
  updateService,
  setServiceActiveStatus,
  ApiError,
  type ServiceSummary,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";

const PAGE_SIZE = 20;
const CURRENCY = "BRL";

const serviceSchema = z.object({
  name: z.string().min(1, "Informe o nome."),
  description: z.string(),
  durationMinutes: z.coerce.number().int("Use um numero inteiro.").min(1, "A duracao precisa ser maior que zero."),
  price: z.coerce.number().min(0, "O preco nao pode ser negativo."),
  category: z.string(),
});

// z.coerce faz o tipo de entrada (antes da validacao) divergir do de saida
// (depois da coercao) — o form precisa ser tipado com os dois generics do RHF
// para o resolver aceitar valores brutos de <input> e devolver numeros no submit.
type ServiceFormValues = z.output<typeof serviceSchema>;
type ServiceFormInput = z.input<typeof serviceSchema>;

const emptyServiceForm: ServiceFormInput = { name: "", description: "", durationMinutes: 30, price: 0, category: "" };

function toNullable(value: string): string | null {
  return value.trim() === "" ? null : value.trim();
}

function formatPrice(amount: number, currency: string): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency }).format(amount);
}

export default function ServicesPage() {
  const router = useRouter();
  const { session } = useSession();
  const queryClient = useQueryClient();

  const [page, setPage] = React.useState(1);
  const [searchInput, setSearchInput] = React.useState("");
  const [search, setSearch] = React.useState("");
  const [dialogOpen, setDialogOpen] = React.useState(false);
  const [editingService, setEditingService] = React.useState<ServiceSummary | null>(null);

  React.useEffect(() => {
    if (!session) {
      router.replace("/login");
    }
  }, [session, router]);

  const accessToken = session?.accessToken ?? "";

  const servicesQuery = useQuery({
    queryKey: ["services", { page, search }],
    queryFn: () => listServices({ page, pageSize: PAGE_SIZE, search: search || undefined }, accessToken),
    enabled: Boolean(session),
    placeholderData: (previous) => previous,
  });

  const form = useForm<ServiceFormInput, unknown, ServiceFormValues>({
    resolver: zodResolver(serviceSchema),
    defaultValues: emptyServiceForm,
  });

  const invalidateList = () => queryClient.invalidateQueries({ queryKey: ["services"] });

  const createMutation = useMutation({
    mutationFn: (values: ServiceFormValues) =>
      createService(
        {
          name: values.name,
          description: toNullable(values.description),
          durationMinutes: values.durationMinutes,
          price: values.price,
          currency: CURRENCY,
          category: toNullable(values.category),
        },
        accessToken
      ),
    onSuccess: () => {
      toast.success("Servico cadastrado.");
      invalidateList();
      setDialogOpen(false);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel cadastrar o servico."),
  });

  const updateMutation = useMutation({
    mutationFn: (values: ServiceFormValues) => {
      if (!editingService) {
        throw new Error("Nenhum servico selecionado.");
      }
      return updateService(
        editingService.id,
        {
          name: values.name,
          description: toNullable(values.description),
          durationMinutes: values.durationMinutes,
          price: values.price,
          currency: CURRENCY,
          category: toNullable(values.category),
        },
        accessToken
      );
    },
    onSuccess: () => {
      toast.success("Servico atualizado.");
      invalidateList();
      setDialogOpen(false);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel atualizar o servico."),
  });

  const statusMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setServiceActiveStatus(id, isActive, accessToken),
    onSuccess: invalidateList,
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel atualizar o status."),
  });

  const totalPages = Math.max(1, Math.ceil((servicesQuery.data?.totalCount ?? 0) / PAGE_SIZE));

  function openCreateDialog() {
    setEditingService(null);
    form.reset(emptyServiceForm);
    setDialogOpen(true);
  }

  async function openEditDialog(service: ServiceSummary) {
    try {
      const details = await getServiceById(service.id, accessToken);
      setEditingService(service);
      form.reset({
        name: details.name,
        description: details.description ?? "",
        durationMinutes: details.durationMinutes,
        price: details.price,
        category: details.category ?? "",
      });
      setDialogOpen(true);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel carregar o servico.");
    }
  }

  if (!session) {
    return null;
  }

  return (
    <main className="mx-auto flex min-h-full w-full max-w-4xl flex-1 flex-col p-6 sm:p-10">
      <Link href="/painel" className="text-muted-foreground mb-6 inline-flex items-center gap-1.5 text-sm hover:text-foreground">
        <ArrowLeft className="size-4" />
        Voltar
      </Link>

      <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Servicos</h1>
          <p className="text-muted-foreground mt-1 text-sm">Catalogo de servicos oferecidos.</p>
        </div>
        <Button onClick={openCreateDialog}>
          <Plus className="size-4" />
          Novo servico
        </Button>
      </div>

      <div className="mb-4 flex gap-2">
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
        />
        <Button
          variant="outline"
          onClick={() => {
            setPage(1);
            setSearch(searchInput);
          }}
        >
          <Search className="size-4" />
        </Button>
      </div>

      <div className="rounded-lg border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Nome</TableHead>
              <TableHead>Categoria</TableHead>
              <TableHead>Duracao</TableHead>
              <TableHead>Preco</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Acoes</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {servicesQuery.data?.items.length === 0 && (
              <TableRow>
                <TableCell colSpan={6} className="text-muted-foreground py-6 text-center">
                  Nenhum servico encontrado.
                </TableCell>
              </TableRow>
            )}
            {servicesQuery.data?.items.map((service) => (
              <TableRow key={service.id}>
                <TableCell className="font-medium">{service.name}</TableCell>
                <TableCell>{service.category ?? "—"}</TableCell>
                <TableCell>{service.durationMinutes} min</TableCell>
                <TableCell>{formatPrice(service.price, service.currency)}</TableCell>
                <TableCell>
                  <Badge variant={service.isActive ? "default" : "outline"}>
                    {service.isActive ? "Ativo" : "Inativo"}
                  </Badge>
                </TableCell>
                <TableCell className="text-right">
                  <div className="flex justify-end gap-2">
                    <Button variant="ghost" size="sm" onClick={() => openEditDialog(service)}>
                      Editar
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => statusMutation.mutate({ id: service.id, isActive: !service.isActive })}
                    >
                      {service.isActive ? "Desativar" : "Ativar"}
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      <div className="mt-4 flex items-center justify-between text-sm">
        <span className="text-muted-foreground">
          Pagina {servicesQuery.data?.page ?? page} de {totalPages}
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

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{editingService ? "Editar servico" : "Novo servico"}</DialogTitle>
          </DialogHeader>
          <Form {...form}>
            <form
              onSubmit={form.handleSubmit((values) =>
                editingService ? updateMutation.mutate(values) : createMutation.mutate(values)
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
              <FormField
                control={form.control}
                name="category"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Categoria</FormLabel>
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
                  name="durationMinutes"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Duracao (min)</FormLabel>
                      <FormControl>
                        <Input type="number" min={1} {...field} value={field.value as number} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="price"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Preco (R$)</FormLabel>
                      <FormControl>
                        <Input type="number" min={0} step={0.01} {...field} value={field.value as number} />
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
                    <FormMessage />
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
    </main>
  );
}
