"use client";

import Link from "next/link";
import { ExternalLink, Pencil } from "lucide-react";

import { useBrandingDraft } from "@/components/settings/branding/branding-draft-context";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

// Dashboard da Marca -- nao e um formulario, e um resumo com atalhos pras
// abas de edicao de verdade (Aparencia/Conteudo/Informacoes/Pagina publica).
// A previa ao vivo ja fica fixa ao lado (ver branding/layout.tsx) -- esta
// pagina so soma nome/status/link/atalhos, sem duplicar a previa grande.
export default function BrandingOverviewPage() {
  const { draft } = useBrandingDraft();
  const publicPath = `/${draft.slug}`;

  return (
    <Card>
      <CardHeader>
        <CardTitle>{draft.name}</CardTitle>
        <CardDescription>Personalização do seu espaço público.</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <div className="flex flex-wrap items-center gap-3">
          <Badge variant={draft.publicPageEnabled ? "success" : "secondary"}>
            {draft.publicPageEnabled ? "Página publicada" : "Página despublicada"}
          </Badge>
          <code className="bg-muted rounded-md px-2 py-1 text-xs">{publicPath}</code>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline" size="sm">
            <Link href={publicPath} target="_blank" rel="noopener noreferrer">
              <ExternalLink className="size-4" />
              Visualizar página
            </Link>
          </Button>
          <Button asChild size="sm">
            <Link href="/settings/branding/appearance">
              <Pencil className="size-4" />
              Editar aparência
            </Link>
          </Button>
        </div>

        <div className="grid gap-2 sm:grid-cols-2">
          <Link href="/settings/branding/content" className="hover:bg-accent rounded-lg border p-3 text-sm transition-colors">
            <span className="font-medium">Conteúdo</span>
            <p className="text-muted-foreground text-xs">Textos e seções exibidas na home.</p>
          </Link>
          <Link href="/settings/branding/info" className="hover:bg-accent rounded-lg border p-3 text-sm transition-colors">
            <span className="font-medium">Informações</span>
            <p className="text-muted-foreground text-xs">Contato, endereço e horário de funcionamento.</p>
          </Link>
          <Link href="/settings/branding/public" className="hover:bg-accent rounded-lg border p-3 text-sm transition-colors">
            <span className="font-medium">Página pública</span>
            <p className="text-muted-foreground text-xs">URL, publicação e compartilhamento.</p>
          </Link>
          <Link href="/settings/branding/appearance" className="hover:bg-accent rounded-lg border p-3 text-sm transition-colors">
            <span className="font-medium">Aparência</span>
            <p className="text-muted-foreground text-xs">Cores, tipografia, logo e banner.</p>
          </Link>
        </div>
      </CardContent>
    </Card>
  );
}
