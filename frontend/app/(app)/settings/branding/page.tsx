"use client";

import Link from "next/link";
import { ArrowRight, CheckCircle2, ExternalLink, FileText, Globe2, ImageIcon, Info, Palette, Sparkles } from "lucide-react";

import { useBrandingDraft } from "@/components/settings/branding/branding-draft-context";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

const QUICK_ACTIONS = [
  { href: "/settings/branding/appearance", label: "Aparência", description: "Logo, banner, cores e tipografia", icon: Palette },
  { href: "/settings/branding/content", label: "Conteúdo", description: "Textos e seções da página inicial", icon: FileText },
  { href: "/settings/branding/info", label: "Informações", description: "Contato, endereço e funcionamento", icon: Info },
  { href: "/settings/branding/public", label: "Divulgação", description: "Publicação, link e QR Code", icon: Globe2 },
];

export default function BrandingOverviewPage() {
  const { draft } = useBrandingDraft();
  const publicPath = `/${draft.slug}`;
  const checklist = [
    { label: "Logo", complete: Boolean(draft.logoPreviewUrl) },
    { label: "Banner", complete: Boolean(draft.bannerPreviewUrl) },
    { label: "Descrição", complete: Boolean(draft.description.trim()) },
    { label: "Chamada principal", complete: Boolean(draft.homeHeroTitle.trim()) },
  ];
  const completed = checklist.filter((item) => item.complete).length;
  const completion = Math.round((completed / checklist.length) * 100);

  return (
    <div className="flex flex-col gap-5">
      <Card className="overflow-hidden border-primary/20">
        <CardContent className="p-0">
          <div className="relative overflow-hidden p-5 sm:p-6" style={{ backgroundColor: `${draft.primaryColorHex || "#6D3DF5"}18` }}>
            <div aria-hidden className="absolute -right-12 -top-20 size-52 rounded-full bg-primary/10 blur-3xl" />
            <div className="relative flex flex-col justify-between gap-5 sm:flex-row sm:items-center">
              <div className="flex min-w-0 items-center gap-4">
                <div className="relative flex size-14 shrink-0 items-center justify-center overflow-hidden rounded-2xl border bg-background shadow-sm">
                  <Sparkles className="size-6 text-primary" />
                  {draft.logoPreviewUrl && (
                    // eslint-disable-next-line @next/next/no-img-element -- URL dinâmica da API.
                    <img key={draft.logoPreviewUrl} src={draft.logoPreviewUrl} alt="" className="absolute inset-0 size-full bg-background object-contain p-1" onError={(event) => { event.currentTarget.style.display = "none"; }} />
                  )}
                </div>
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <h3 className="truncate text-xl font-semibold tracking-tight">{draft.name}</h3>
                    <Badge variant={draft.publicPageEnabled ? "success" : "secondary"}>{draft.publicPageEnabled ? "Publicada" : "Despublicada"}</Badge>
                  </div>
                  <p className="mt-1 truncate font-mono text-xs text-muted-foreground">{publicPath}</p>
                </div>
              </div>
              <Button asChild variant="outline" className="shrink-0 bg-background/80">
                <Link href={publicPath} target="_blank" rel="noopener noreferrer"><ExternalLink className="size-4" />Abrir página</Link>
              </Button>
            </div>
          </div>

          <div className="grid gap-5 border-t p-5 sm:grid-cols-[1fr_auto] sm:items-center sm:p-6">
            <div>
              <div className="flex items-center justify-between gap-4">
                <div>
                  <p className="text-sm font-medium">Configuração da marca</p>
                  <p className="text-xs text-muted-foreground">{completed} de {checklist.length} itens essenciais preenchidos</p>
                </div>
                <span className="text-sm font-semibold text-primary">{completion}%</span>
              </div>
              <div className="mt-3 h-2 overflow-hidden rounded-full bg-muted"><div className="h-full rounded-full bg-primary transition-all" style={{ width: `${completion}%` }} /></div>
              <div className="mt-3 flex flex-wrap gap-x-4 gap-y-2">
                {checklist.map((item) => (
                  <span key={item.label} className={item.complete ? "flex items-center gap-1.5 text-xs text-emerald-600 dark:text-emerald-400" : "flex items-center gap-1.5 text-xs text-muted-foreground"}>
                    {item.complete ? <CheckCircle2 className="size-3.5" /> : <ImageIcon className="size-3.5" />}{item.label}
                  </span>
                ))}
              </div>
            </div>
            <Button asChild size="sm"><Link href="/settings/branding/appearance">Continuar personalizando<ArrowRight className="size-4" /></Link></Button>
          </div>
        </CardContent>
      </Card>

      <div>
        <div className="mb-3"><h3 className="font-semibold">Personalize sua presença online</h3><p className="text-sm text-muted-foreground">Escolha uma área para editar.</p></div>
        <div className="grid gap-3 sm:grid-cols-2">
          {QUICK_ACTIONS.map((action) => {
            const Icon = action.icon;
            return (
              <Link key={action.href} href={action.href} className="group flex items-center gap-3 rounded-xl border bg-card p-4 shadow-xs transition-all hover:-translate-y-0.5 hover:border-primary/35 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
                <div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary transition-colors group-hover:bg-primary group-hover:text-primary-foreground"><Icon className="size-4.5" /></div>
                <div className="min-w-0 flex-1"><p className="font-medium">{action.label}</p><p className="truncate text-xs text-muted-foreground">{action.description}</p></div>
                <ArrowRight className="size-4 text-muted-foreground transition-transform group-hover:translate-x-0.5 group-hover:text-primary" />
              </Link>
            );
          })}
        </div>
      </div>
    </div>
  );
}
