"use client";

import * as React from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Plus, Search, Sparkles } from "lucide-react";

import {
  listServices,
  getServiceById,
  createService,
  updateService,
  setServiceActiveStatus,
  uploadServiceImage,
  resolveAssetUrl,
  ApiError,
  type ServiceSummary,
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
const CURRENCY = "BRL";

const ALLOWED_IMAGE_TYPES = ["image/png", "image/jpeg", "image/webp"];
const MAX_IMAGE_SIZE_BYTES = 2 * 1024 * 1024;

const serviceSchema = z.object({
  name: z.string().min(1, "Informe o nome."),
  description: z.string(),
  durationMinutes: z.coerce.number().int("Use um numero inteiro.").min(1, "A duracao precisa ser maior que zero."),
  price: z.coerce.number().min(0, "O preco nao pode ser negativo."),
  category: z.string(),
  displayOrder: z.coerce.number().int("Use um numero inteiro.").min(0, "A ordem nao pode ser negativa."),
});

// z.coerce faz o tipo de entrada (antes da validacao) divergir do de saida
// (depois da coercao) — o form precisa ser tipado com os dois generics do RHF
// para o resolver aceitar valores brutos de <input> e devolver numeros no submit.
type ServiceFormValues = z.output<typeof serviceSchema>;
type ServiceFormInput = z.input<typeof serviceSchema>;

const emptyServiceForm: ServiceFormInput = {
  name: "",
  description: "",
  durationMinutes: 30,
  price: 0,
  category: "",
  displayOrder: 0,
};

function toNullable(value: string): string | null {
  return value.trim() === "" ? null : value.trim();
}

function formatPrice(amount: number, currency: string): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency }).format(amount);
}

export default function ServicesPage() {
  const { session } = useSession();
  const queryClient = useQueryClient();

  const [page, setPage] = React.useState(1);
  const [searchInput, setSearchInput] = React.useState("");
  const [search, setSearch] = React.useState("");
  const [dialogOpen, setDialogOpen] = React.useState(false);
  const [editingService, setEditingService] = React.useState<ServiceSummary | null>(null);
  const [imagePreviewUrl, setImagePreviewUrl] = React.useState<string | null>(null);
  const [selectedImageFile, setSelectedImageFile] = React.useState<File | null>(null);
  const [isUploadingImage, setIsUploadingImage] = React.useState(false);
  const imageInputRef = React.useRef<HTMLInputElement>(null);

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
          displayOrder: values.displayOrder,
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
          displayOrder: values.displayOrder,
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
    setImagePreviewUrl(null);
    setSelectedImageFile(null);
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
        displayOrder: details.displayOrder,
      });
      setImagePreviewUrl(details.imageUrl ? resolveAssetUrl(details.imageUrl) : null);
      setSelectedImageFile(null);
      setDialogOpen(true);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel carregar o servico.");
    }
  }

  function handleImageFileChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
      toast.error("Formato invalido. Envie um arquivo PNG, JPEG ou WEBP.");
      return;
    }

    if (file.size > MAX_IMAGE_SIZE_BYTES) {
      toast.error("O arquivo nao pode ter mais que 2MB.");
      return;
    }

    setSelectedImageFile(file);
    setImagePreviewUrl(URL.createObjectURL(file));
  }

  async function handleImageUpload() {
    if (!selectedImageFile || !editingService) {
      return;
    }

    setIsUploadingImage(true);
    try {
      const result = await uploadServiceImage(editingService.id, selectedImageFile, accessToken);
      setImagePreviewUrl(resolveAssetUrl(result.imageUrl));
      setSelectedImageFile(null);
      invalidateList();
      toast.success("Imagem atualizada.");
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel enviar a imagem.");
    } finally {
      setIsUploadingImage(false);
    }
  }

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-1 flex-col">
      <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
        <p className="text-muted-foreground text-sm">Catalogo de servicos oferecidos.</p>
        <Button onClick={openCreateDialog}>
          <Plus className="size-4" />
          Novo servico
        </Button>
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
              aria-label="Buscar servicos"
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
                <TableHead>Categoria</TableHead>
                <TableHead>Duracao</TableHead>
                <TableHead>Preco</TableHead>
                <TableHead>Ordem</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="text-right">Acoes</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {servicesQuery.isLoading ? (
                Array.from({ length: 5 }).map((_, index) => (
                  <TableRow key={index}>
                    <TableCell><Skeleton className="h-4 w-32" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-24" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-16" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-20" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-10" /></TableCell>
                    <TableCell><Skeleton className="h-5 w-14 rounded-full" /></TableCell>
                    <TableCell className="text-right"><Skeleton className="ml-auto h-4 w-20" /></TableCell>
                  </TableRow>
                ))
              ) : servicesQuery.data?.items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={7} className="p-0">
                    {search ? (
                      <EmptyState
                        icon={Search}
                        title={`Nenhum servico encontrado para "${search}"`}
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
                        icon={Sparkles}
                        title="Nenhum servico cadastrado ainda"
                        description="Cadastre o primeiro servico do seu catalogo."
                        action={
                          <Button size="sm" onClick={openCreateDialog}>
                            <Plus className="size-4" />
                            Novo servico
                          </Button>
                        }
                      />
                    )}
                  </TableCell>
                </TableRow>
              ) : (
                servicesQuery.data?.items.map((service) => (
                  <TableRow key={service.id}>
                    <TableCell className="font-medium">
                      <div className="flex items-center gap-2">
                        {service.imageUrl ? (
                          // eslint-disable-next-line @next/next/no-img-element -- miniatura de URL dinamica da API, nao um asset estatico do build.
                          <img
                            src={resolveAssetUrl(service.imageUrl)}
                            alt=""
                            className="bg-muted size-8 shrink-0 rounded-md object-cover"
                          />
                        ) : (
                          <div className="bg-muted flex size-8 shrink-0 items-center justify-center rounded-md">
                            <Sparkles className="text-muted-foreground size-4" />
                          </div>
                        )}
                        {service.name}
                      </div>
                    </TableCell>
                    <TableCell>{service.category ?? "—"}</TableCell>
                    <TableCell>{service.durationMinutes} min</TableCell>
                    <TableCell>{formatPrice(service.price, service.currency)}</TableCell>
                    <TableCell>{service.displayOrder}</TableCell>
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
                ))
              )}
            </TableBody>
          </Table>

          <div className="flex items-center justify-between text-sm">
            <span className="text-muted-foreground" aria-live="polite">
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
        </CardContent>
      </Card>

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
              <div className="grid grid-cols-3 gap-3">
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
                <FormField
                  control={form.control}
                  name="displayOrder"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Ordem</FormLabel>
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
                    <FormMessage />
                  </FormItem>
                )}
              />
              {editingService && (
                <div className="grid gap-2">
                  <FormLabel>Imagem</FormLabel>
                  <div className="flex items-center gap-4">
                    <div className="bg-muted flex size-16 shrink-0 items-center justify-center overflow-hidden rounded-lg border">
                      {imagePreviewUrl ? (
                        // eslint-disable-next-line @next/next/no-img-element -- preview de upload local/URL dinamica da API, nao um asset estatico do build.
                        <img src={imagePreviewUrl} alt="" className="size-full object-cover" />
                      ) : (
                        <span className="text-muted-foreground text-xs">Sem imagem</span>
                      )}
                    </div>
                    <div className="flex flex-col gap-2">
                      <input
                        ref={imageInputRef}
                        type="file"
                        accept="image/png,image/jpeg,image/webp"
                        onChange={handleImageFileChange}
                        className="hidden"
                      />
                      <div className="flex gap-2">
                        <Button type="button" variant="outline" size="sm" onClick={() => imageInputRef.current?.click()}>
                          Escolher arquivo
                        </Button>
                        {selectedImageFile && (
                          <Button type="button" size="sm" onClick={handleImageUpload} disabled={isUploadingImage}>
                            {isUploadingImage ? "Enviando..." : "Enviar"}
                          </Button>
                        )}
                      </div>
                      <p className="text-muted-foreground text-xs">PNG, JPEG ou WEBP, ate 2MB.</p>
                    </div>
                  </div>
                </div>
              )}
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
