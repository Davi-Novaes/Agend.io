"use client";

import * as React from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Plus, Search, Upload, Users } from "lucide-react";

import {
  listCustomers,
  getCustomerById,
  createCustomer,
  updateCustomer,
  setCustomerActiveStatus,
  importCustomersFromCsv,
  ApiError,
  type CustomerSummary,
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
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";

const PAGE_SIZE = 20;

const customerSchema = z.object({
  fullName: z.string().min(1, "Informe o nome."),
  email: z.union([z.email("E-mail invalido."), z.literal("")]),
  phone: z.string(),
  notes: z.string(),
  dateOfBirth: z.string(),
  cpf: z.string(),
  healthNotes: z.string(),
});

type CustomerFormValues = z.infer<typeof customerSchema>;

const emptyCustomerForm: CustomerFormValues = {
  fullName: "",
  email: "",
  phone: "",
  notes: "",
  dateOfBirth: "",
  cpf: "",
  healthNotes: "",
};

function toNullable(value: string): string | null {
  return value.trim() === "" ? null : value.trim();
}

export default function CustomersPage() {
  const { session } = useSession();
  const queryClient = useQueryClient();

  const [page, setPage] = React.useState(1);
  const [searchInput, setSearchInput] = React.useState("");
  const [search, setSearch] = React.useState("");
  const [dialogOpen, setDialogOpen] = React.useState(false);
  const [editingCustomer, setEditingCustomer] = React.useState<CustomerSummary | null>(null);
  const importInputRef = React.useRef<HTMLInputElement>(null);

  const accessToken = session?.accessToken ?? "";

  const customersQuery = useQuery({
    queryKey: ["customers", { page, search }],
    queryFn: () => listCustomers({ page, pageSize: PAGE_SIZE, search: search || undefined }, accessToken),
    enabled: Boolean(session),
    placeholderData: (previous) => previous,
  });

  const form = useForm<CustomerFormValues>({
    resolver: zodResolver(customerSchema),
    defaultValues: emptyCustomerForm,
  });

  const invalidateList = () => queryClient.invalidateQueries({ queryKey: ["customers"] });

  const createMutation = useMutation({
    mutationFn: (values: CustomerFormValues) =>
      createCustomer(
        {
          fullName: values.fullName,
          email: toNullable(values.email),
          phone: toNullable(values.phone),
          notes: toNullable(values.notes),
          dateOfBirth: toNullable(values.dateOfBirth),
          cpf: toNullable(values.cpf),
          healthNotes: toNullable(values.healthNotes),
        },
        accessToken
      ),
    onSuccess: () => {
      toast.success("Cliente cadastrado.");
      invalidateList();
      setDialogOpen(false);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel cadastrar o cliente."),
  });

  const updateMutation = useMutation({
    mutationFn: (values: CustomerFormValues) => {
      if (!editingCustomer) {
        throw new Error("Nenhum cliente selecionado.");
      }
      return updateCustomer(
        editingCustomer.id,
        {
          fullName: values.fullName,
          email: toNullable(values.email),
          phone: toNullable(values.phone),
          notes: toNullable(values.notes),
          dateOfBirth: toNullable(values.dateOfBirth),
          cpf: toNullable(values.cpf),
          healthNotes: toNullable(values.healthNotes),
        },
        accessToken
      );
    },
    onSuccess: () => {
      toast.success("Cliente atualizado.");
      invalidateList();
      setDialogOpen(false);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel atualizar o cliente."),
  });

  const statusMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setCustomerActiveStatus(id, isActive, accessToken),
    onSuccess: invalidateList,
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel atualizar o status."),
  });

  const importMutation = useMutation({
    mutationFn: (file: File) => importCustomersFromCsv(file, accessToken),
    onSuccess: (result) => {
      toast.success(`Importacao concluida: ${result.imported} importado(s), ${result.skipped} ignorado(s).`);
      if (result.errors.length > 0) {
        toast.error(result.errors.slice(0, 3).join(" "));
      }
      invalidateList();
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel importar o arquivo."),
    onSettled: () => {
      if (importInputRef.current) {
        importInputRef.current.value = "";
      }
    },
  });

  const totalPages = Math.max(1, Math.ceil((customersQuery.data?.totalCount ?? 0) / PAGE_SIZE));

  function openCreateDialog() {
    setEditingCustomer(null);
    form.reset(emptyCustomerForm);
    setDialogOpen(true);
  }

  async function openEditDialog(customer: CustomerSummary) {
    try {
      const details = await getCustomerById(customer.id, accessToken);
      setEditingCustomer(customer);
      form.reset({
        fullName: details.fullName,
        email: details.email ?? "",
        phone: details.phone ?? "",
        notes: details.notes ?? "",
        dateOfBirth: details.dateOfBirth ?? "",
        cpf: details.cpf ?? "",
        healthNotes: details.healthNotes ?? "",
      });
      setDialogOpen(true);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel carregar o cliente.");
    }
  }

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-1 flex-col">
      <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
        <p className="text-muted-foreground text-sm">Cadastro de clientes do seu estabelecimento.</p>
        <div className="flex gap-2">
          <input
            ref={importInputRef}
            type="file"
            accept=".csv"
            className="hidden"
            onChange={(event) => {
              const file = event.target.files?.[0];
              if (file) {
                importMutation.mutate(file);
              }
            }}
          />
          <Button variant="outline" onClick={() => importInputRef.current?.click()} disabled={importMutation.isPending}>
            <Upload className="size-4" />
            {importMutation.isPending ? "Importando..." : "Importar CSV"}
          </Button>
          <Button onClick={openCreateDialog}>
            <Plus className="size-4" />
            Novo cliente
          </Button>
        </div>
      </div>

      <Card>
        <CardContent className="flex flex-col gap-4">
          <div className="flex gap-2">
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
              size="icon"
              aria-label="Buscar clientes"
              onClick={() => {
                setPage(1);
                setSearch(searchInput);
              }}
            >
              <Search className="size-4" />
            </Button>
          </div>

          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Nome</TableHead>
                <TableHead>E-mail</TableHead>
                <TableHead>Telefone</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="text-right">Acoes</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {customersQuery.isLoading ? (
                Array.from({ length: 5 }).map((_, index) => (
                  <TableRow key={index}>
                    <TableCell><Skeleton className="h-4 w-32" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-40" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-24" /></TableCell>
                    <TableCell><Skeleton className="h-5 w-14 rounded-full" /></TableCell>
                    <TableCell className="text-right"><Skeleton className="ml-auto h-4 w-20" /></TableCell>
                  </TableRow>
                ))
              ) : customersQuery.data?.items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} className="p-0">
                    {search ? (
                      <EmptyState
                        icon={Search}
                        title={`Nenhum cliente encontrado para "${search}"`}
                        description="Tente ajustar os termos da busca ou limpe o filtro."
                        action={
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => {
                              setSearchInput("");
                              setSearch("");
                              setPage(1);
                            }}
                          >
                            Limpar busca
                          </Button>
                        }
                      />
                    ) : (
                      <EmptyState
                        icon={Users}
                        title="Nenhum cliente cadastrado ainda"
                        description="Cadastre seu primeiro cliente ou importe uma lista via CSV."
                        action={
                          <Button size="sm" onClick={openCreateDialog}>
                            <Plus className="size-4" />
                            Novo cliente
                          </Button>
                        }
                      />
                    )}
                  </TableCell>
                </TableRow>
              ) : (
                customersQuery.data?.items.map((customer) => (
                  <TableRow key={customer.id}>
                    <TableCell className="font-medium">{customer.fullName}</TableCell>
                    <TableCell>{customer.email ?? "—"}</TableCell>
                    <TableCell>{customer.phone ?? "—"}</TableCell>
                    <TableCell>
                      <Badge variant={customer.isActive ? "default" : "outline"}>
                        {customer.isActive ? "Ativo" : "Inativo"}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-2">
                        <Button variant="ghost" size="sm" onClick={() => openEditDialog(customer)}>
                          Editar
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => statusMutation.mutate({ id: customer.id, isActive: !customer.isActive })}
                        >
                          {customer.isActive ? "Desativar" : "Ativar"}
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>

          <div className="flex items-center justify-between text-sm">
            <span className="text-muted-foreground" aria-live="polite">
              Pagina {customersQuery.data?.page ?? page} de {totalPages}
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
            <DialogTitle>{editingCustomer ? "Editar cliente" : "Novo cliente"}</DialogTitle>
          </DialogHeader>
          <Form {...form}>
            <form
              onSubmit={form.handleSubmit((values) =>
                editingCustomer ? updateMutation.mutate(values) : createMutation.mutate(values)
              )}
              className="flex flex-col gap-3"
            >
              <FormField
                control={form.control}
                name="fullName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Nome completo</FormLabel>
                    <FormControl>
                      <Input {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="email"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>E-mail</FormLabel>
                    <FormControl>
                      <Input type="email" {...field} />
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
                      <Input {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="dateOfBirth"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Data de nascimento</FormLabel>
                    <FormControl>
                      <Input type="date" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="cpf"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>CPF</FormLabel>
                    <FormControl>
                      <Input placeholder="Opcional" autoComplete="off" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="notes"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Observacoes</FormLabel>
                    <FormControl>
                      <Textarea rows={3} {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="healthNotes"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Dados de saude (convenio, alergia, restricao)</FormLabel>
                    <FormControl>
                      <Textarea rows={2} placeholder="Opcional" {...field} />
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
    </div>
  );
}
