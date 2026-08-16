"use client";

import * as React from "react";
import { use as usePromise } from "react";
import Link from "next/link";
import { CheckCircle2, XCircle } from "lucide-react";

import { confirmEmail, ApiError } from "@/lib/api/client";
import { Logo } from "@/components/logo";
import { Button } from "@/components/ui/button";

type PageProps = {
  params: Promise<{ token: string }>;
};

type Status = "confirming" | "success" | "error";

export default function ConfirmEmailPage({ params }: PageProps) {
  const { token } = usePromise(params);
  const [status, setStatus] = React.useState<Status>("confirming");
  const [errorMessage, setErrorMessage] = React.useState<string | null>(null);

  React.useEffect(() => {
    let cancelled = false;

    confirmEmail({ token })
      .then(() => {
        if (!cancelled) {
          setStatus("success");
        }
      })
      .catch((error: unknown) => {
        if (cancelled) {
          return;
        }
        setErrorMessage(
          error instanceof ApiError ? error.message : "Nao foi possivel confirmar seu e-mail. Tente novamente."
        );
        setStatus("error");
      });

    return () => {
      cancelled = true;
    };
  }, [token]);

  return (
    <main className="flex min-h-full flex-1 flex-col items-center justify-center gap-3 p-4 text-center">
      <div className="mb-2">
        <Logo />
      </div>

      {status === "confirming" && (
        <p className="text-muted-foreground text-sm">Confirmando seu e-mail...</p>
      )}

      {status === "success" && (
        <>
          <CheckCircle2 className="text-primary size-10" strokeWidth={1.5} />
          <h1 className="text-xl font-semibold tracking-tight">E-mail confirmado!</h1>
          <p className="text-muted-foreground max-w-sm text-sm">
            Sua conta ja esta ativa. Entre com seu e-mail e senha para acessar o painel.
          </p>
          <Button asChild className="mt-2">
            <Link href="/login">Ir para o login</Link>
          </Button>
        </>
      )}

      {status === "error" && (
        <>
          <XCircle className="text-destructive size-10" strokeWidth={1.5} />
          <h1 className="text-xl font-semibold tracking-tight">Link invalido ou expirado</h1>
          <p className="text-muted-foreground max-w-sm text-sm">{errorMessage}</p>
          <p className="text-muted-foreground max-w-sm text-sm">
            Faca login normalmente — se seu e-mail ainda nao estiver confirmado, voce podera pedir um novo link
            por la.
          </p>
          <Button asChild variant="outline" className="mt-2">
            <Link href="/login">Ir para o login</Link>
          </Button>
        </>
      )}
    </main>
  );
}
