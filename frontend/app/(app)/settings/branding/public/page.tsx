"use client";

import * as React from "react";
import Link from "next/link";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import QRCode from "qrcode";
import { Download, Link2, MessageCircle, Share2, Camera, ExternalLink } from "lucide-react";

import { updateTenantPublicPageStatus, TENANT_PROFILE_QUERY_KEY, ApiError } from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { useBrandingDraft } from "@/components/settings/branding/branding-draft-context";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Switch } from "@/components/ui/switch";
import { Label } from "@/components/ui/label";

// Hierarquia label/valor: rotulo pequeno/discreto/uppercase, valor grande/forte.
const FIELD_LABEL_CLASS = "text-[13px] font-medium tracking-wide text-muted-foreground uppercase";

// window.location.origin so ha no cliente — useSyncExternalStore evita hidratacao
// divergente (SSR sempre "") sem cair no lint de setState sincrono dentro de efeito.
function subscribeToNothing() {
  return () => {};
}
function getWindowOrigin() {
  return window.location.origin;
}
function getServerOrigin() {
  return "";
}

export default function BrandingPublicPagePage() {
  const { session } = useSession();
  const accessToken = session?.accessToken ?? "";
  const queryClient = useQueryClient();
  const { draft, update } = useBrandingDraft();

  const [isSavingStatus, setIsSavingStatus] = React.useState(false);
  const [qrCodeDataUrl, setQrCodeDataUrl] = React.useState<string | null>(null);

  const origin = React.useSyncExternalStore(subscribeToNothing, getWindowOrigin, getServerOrigin);
  const publicUrl = origin ? `${origin}/${draft.slug}` : "";

  React.useEffect(() => {
    if (!publicUrl) return;
    let cancelled = false;
    QRCode.toDataURL(`${publicUrl}?ref=qrcode`, { width: 240, margin: 1 }).then((dataUrl) => {
      if (!cancelled) setQrCodeDataUrl(dataUrl);
    });
    return () => {
      cancelled = true;
    };
  }, [publicUrl]);

  function buildShareUrl(ref: string) {
    return publicUrl ? `${publicUrl}?ref=${ref}` : "";
  }

  function handleCopyShareLink(ref: string, message: string) {
    navigator.clipboard.writeText(buildShareUrl(ref));
    toast.success(message);
  }

  async function handleTogglePublished(enabled: boolean) {
    setIsSavingStatus(true);
    update({ publicPageEnabled: enabled });
    try {
      await updateTenantPublicPageStatus(enabled, accessToken);
      await queryClient.invalidateQueries({ queryKey: TENANT_PROFILE_QUERY_KEY });
      toast.success(enabled ? "Página publicada." : "Página despublicada.");
    } catch (error) {
      update({ publicPageEnabled: !enabled });
      toast.error(error instanceof ApiError ? error.message : "Não foi possível atualizar o status da página.");
    } finally {
      setIsSavingStatus(false);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">URL e publicação</CardTitle>
          <CardDescription>Endereço público do seu estabelecimento e se ele está disponível para clientes.</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <div className="grid gap-1.5">
            <Label className={FIELD_LABEL_CLASS}>URL pública</Label>
            <div className="bg-muted flex items-center justify-between gap-2 rounded-lg p-3">
              <span className="truncate text-[15px] font-semibold text-foreground">{publicUrl || "Carregando link..."}</span>
              <Button type="button" variant="ghost" size="icon" aria-label="Copiar link" disabled={!publicUrl} onClick={() => handleCopyShareLink("link", "Link copiado.")}>
                <Link2 className="size-4" />
              </Button>
            </div>
          </div>

          <div className="flex items-center justify-between gap-4">
            <div>
              <p className="text-sm font-medium">Página publicada</p>
              <p className="text-muted-foreground text-xs">Desligue para tirar o portal do ar sem apagar nenhum dado.</p>
            </div>
            <Switch checked={draft.publicPageEnabled} onCheckedChange={handleTogglePublished} disabled={isSavingStatus} aria-label="Publicar página pública" />
          </div>

          <Button asChild variant="outline" className="w-fit">
            <Link href={`/${draft.slug}`} target="_blank" rel="noopener noreferrer">
              <ExternalLink className="size-4" />
              Visualizar página pública
            </Link>
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Compartilhar minha página</CardTitle>
          <CardDescription>QR Code e links prontos para divulgar seu estabelecimento.</CardDescription>
        </CardHeader>
        {/*
          @container em vez de "sm:" (viewport): esta coluna pode ficar
          estreita mesmo em telas largas (grid de duas colunas com a previa
          ao lado), entao "sm:" nunca refletia a largura real disponivel.
        */}
        <CardContent className="@container grid gap-4 @sm:grid-cols-[auto_1fr]">
          <div className="flex flex-col items-center gap-2">
            <div className="bg-muted flex size-40 shrink-0 items-center justify-center overflow-hidden rounded-lg border">
              {qrCodeDataUrl ? (
                // eslint-disable-next-line @next/next/no-img-element -- data URL gerada localmente.
                <img src={qrCodeDataUrl} alt="QR Code da página pública" className="size-full object-contain" />
              ) : (
                <span className="text-muted-foreground text-xs">Gerando QR Code...</span>
              )}
            </div>
            <Button asChild variant="outline" size="sm" disabled={!qrCodeDataUrl}>
              <a href={qrCodeDataUrl ?? undefined} download={`qrcode-${draft.slug}.png`}>
                <Download className="size-4" />
                Baixar QR Code
              </a>
            </Button>
          </div>

          <div className="flex flex-col gap-3">
            <div className="flex flex-wrap gap-2">
              <Button asChild variant="outline" disabled={!publicUrl}>
                <a
                  href={`https://wa.me/?text=${encodeURIComponent(`Agende com ${draft.name}: ${buildShareUrl("whatsapp")}`)}`}
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  <MessageCircle className="size-4" />
                  WhatsApp
                </a>
              </Button>
              <Button asChild variant="outline" disabled={!publicUrl}>
                <a href={`https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(buildShareUrl("facebook"))}`} target="_blank" rel="noopener noreferrer">
                  <Share2 className="size-4" />
                  Facebook
                </a>
              </Button>
              <Button type="button" variant="outline" disabled={!publicUrl} onClick={() => handleCopyShareLink("instagram", "Link copiado - cole na bio ou em um story do Instagram.")}>
                <Camera className="size-4" />
                Instagram
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
