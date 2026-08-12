"use client";

import * as React from "react";
import { toast } from "sonner";

import { updateTenantBranding, uploadTenantLogo, resolveAssetUrl, ApiError } from "@/lib/api/client";
import { meetsAaContrast, contrastRatio } from "@/lib/tenant/contrast";
import { useSession } from "@/lib/auth/session-context";
import { DEFAULT_TENANT_THEME } from "@/lib/tenant/tenant-theme";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";

const ALLOWED_LOGO_TYPES = ["image/png", "image/jpeg", "image/webp"];
const MAX_LOGO_SIZE_BYTES = 2 * 1024 * 1024;

// A UI sempre pareia --primary com texto branco (ver app/globals.css) — o
// contraste e verificado contra ESTA cor fixa, tanto aqui quanto no backend.
const FOREGROUND_HEX = "#FFFFFF";

function toHex(oklchOrHex: string): string {
  return oklchOrHex.startsWith("#") ? oklchOrHex : "#3730A3";
}

export default function BrandingSettingsPage() {
  const { session } = useSession();
  const [color, setColor] = React.useState(() => toHex(DEFAULT_TENANT_THEME.primary));
  const [isSaving, setIsSaving] = React.useState(false);
  const [logoPreviewUrl, setLogoPreviewUrl] = React.useState<string | null>(null);
  const [selectedLogoFile, setSelectedLogoFile] = React.useState<File | null>(null);
  const [isUploadingLogo, setIsUploadingLogo] = React.useState(false);
  const logoInputRef = React.useRef<HTMLInputElement>(null);

  React.useEffect(() => {
    return () => {
      if (logoPreviewUrl) {
        URL.revokeObjectURL(logoPreviewUrl);
      }
    };
  }, [logoPreviewUrl]);

  if (!session) {
    return null;
  }

  function handleLogoFileChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    if (!ALLOWED_LOGO_TYPES.includes(file.type)) {
      toast.error("Formato invalido. Envie um arquivo PNG, JPEG ou WEBP.");
      return;
    }

    if (file.size > MAX_LOGO_SIZE_BYTES) {
      toast.error("O arquivo nao pode ter mais que 2MB.");
      return;
    }

    setSelectedLogoFile(file);
    setLogoPreviewUrl(URL.createObjectURL(file));
  }

  async function handleLogoUpload() {
    if (!selectedLogoFile) {
      return;
    }

    setIsUploadingLogo(true);
    try {
      const result = await uploadTenantLogo(selectedLogoFile, session!.accessToken);
      setLogoPreviewUrl(resolveAssetUrl(result.logoUrl));
      setSelectedLogoFile(null);
      toast.success("Logo atualizado.");
    } catch (error) {
      const message = error instanceof ApiError ? error.message : "Nao foi possivel enviar o logo.";
      toast.error(message);
    } finally {
      setIsUploadingLogo(false);
    }
  }

  const ratio = contrastRatio(FOREGROUND_HEX, color);
  const passesAa = meetsAaContrast(FOREGROUND_HEX, color);

  async function handleSave() {
    setIsSaving(true);
    try {
      await updateTenantBranding(color, session!.accessToken);
      toast.success("Cor de marca atualizada.");
    } catch (error) {
      const message =
        error instanceof ApiError ? error.message : "Nao foi possivel salvar a cor de marca.";
      toast.error(message);
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <div className="mx-auto flex w-full max-w-lg flex-1 flex-col gap-6">
      <p className="text-muted-foreground text-sm">
        Escolha a cor principal do seu painel e do portal de agendamento.
      </p>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Logo</CardTitle>
          <CardDescription>Aparece no painel e no portal publico do seu estabelecimento.</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex items-center gap-4">
            <div className="bg-muted flex size-16 shrink-0 items-center justify-center overflow-hidden rounded-lg border">
              {logoPreviewUrl ? (
                // eslint-disable-next-line @next/next/no-img-element -- preview de upload local/URL dinamica da API, nao um asset estatico do build.
                <img src={logoPreviewUrl} alt="Logo do estabelecimento" className="size-full object-contain" />
              ) : (
                <span className="text-muted-foreground text-xs">Sem logo</span>
              )}
            </div>
            <div className="flex flex-col gap-2">
              <input
                ref={logoInputRef}
                type="file"
                accept="image/png,image/jpeg,image/webp"
                onChange={handleLogoFileChange}
                className="hidden"
              />
              <div className="flex gap-2">
                <Button type="button" variant="outline" onClick={() => logoInputRef.current?.click()}>
                  Escolher arquivo
                </Button>
                {selectedLogoFile && (
                  <Button type="button" onClick={handleLogoUpload} disabled={isUploadingLogo}>
                    {isUploadingLogo ? "Enviando..." : "Enviar"}
                  </Button>
                )}
              </div>
              <p className="text-muted-foreground text-xs">PNG, JPEG ou WEBP, ate 2MB.</p>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Cor de marca</CardTitle>
          <CardDescription>Usada em botoes e destaques no painel e no portal publico.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4">
          <div className="flex items-center gap-3">
            <input
              type="color"
              value={color}
              onChange={(event) => setColor(event.target.value.toUpperCase())}
              className="h-10 w-14 cursor-pointer rounded-md border border-input"
              aria-label="Selecionar cor de marca"
            />
            <div className="grid gap-1">
              <Label htmlFor="color-hex">Codigo da cor</Label>
              <input
                id="color-hex"
                value={color}
                onChange={(event) => setColor(event.target.value.toUpperCase())}
                maxLength={7}
                className="border-input bg-background h-8 w-28 rounded-md border px-2 font-mono text-sm outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
              />
            </div>
          </div>

          <div
            className="flex h-16 items-center justify-center rounded-lg text-sm font-medium"
            style={{ backgroundColor: color, color: FOREGROUND_HEX }}
          >
            Assim vai ficar um botao no seu painel
          </div>

          <p
            className={
              passesAa
                ? "text-sm text-emerald-700 dark:text-emerald-400"
                : "text-destructive text-sm"
            }
            role="status"
          >
            Contraste com o texto branco: {ratio.toFixed(1)}:1 —{" "}
            {passesAa ? "atende ao padrao de acessibilidade (AA)." : "insuficiente. Escolha um tom mais escuro."}
          </p>

          <Button onClick={handleSave} disabled={isSaving || !passesAa} className="mt-2 w-fit">
            {isSaving ? "Salvando..." : "Salvar cor de marca"}
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
