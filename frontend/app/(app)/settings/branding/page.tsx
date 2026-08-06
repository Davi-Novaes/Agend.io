"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { toast } from "sonner";
import { ArrowLeft } from "lucide-react";

import { updateTenantBranding, ApiError } from "@/lib/api/client";
import { meetsAaContrast, contrastRatio } from "@/lib/tenant/contrast";
import { useSession } from "@/lib/auth/session-context";
import { DEFAULT_TENANT_THEME } from "@/lib/tenant/tenant-theme";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";

// A UI sempre pareia --primary com texto branco (ver app/globals.css) — o
// contraste e verificado contra ESTA cor fixa, tanto aqui quanto no backend.
const FOREGROUND_HEX = "#FFFFFF";

function toHex(oklchOrHex: string): string {
  return oklchOrHex.startsWith("#") ? oklchOrHex : "#3730A3";
}

export default function BrandingSettingsPage() {
  const router = useRouter();
  const { session } = useSession();
  const [color, setColor] = React.useState(() => toHex(DEFAULT_TENANT_THEME.primary));
  const [isSaving, setIsSaving] = React.useState(false);

  React.useEffect(() => {
    if (!session) {
      router.replace("/login");
    }
  }, [session, router]);

  if (!session) {
    return null;
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
    <main className="mx-auto flex min-h-full w-full max-w-lg flex-1 flex-col p-6 sm:p-10">
      <Link href="/" className="text-muted-foreground mb-6 inline-flex items-center gap-1.5 text-sm hover:text-foreground">
        <ArrowLeft className="size-4" />
        Voltar
      </Link>

      <h1 className="text-xl font-semibold tracking-tight">Marca</h1>
      <p className="text-muted-foreground mt-1 mb-6 text-sm">
        Escolha a cor principal do seu painel e do portal de agendamento.
      </p>

      <div className="grid gap-4">
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
      </div>
    </main>
  );
}
