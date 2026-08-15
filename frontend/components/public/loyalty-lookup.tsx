"use client";

import * as React from "react";
import { useMutation } from "@tanstack/react-query";
import { Gift } from "lucide-react";

import { getPublicLoyaltyStatus, ApiError, type PublicLoyaltyStatus } from "@/lib/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

export function LoyaltyLookup({ tenantId, buttonRadiusClassName }: { tenantId: string; buttonRadiusClassName?: string }) {
  const [email, setEmail] = React.useState("");
  const [result, setResult] = React.useState<PublicLoyaltyStatus | null>(null);
  const [notFound, setNotFound] = React.useState(false);

  const mutation = useMutation({
    mutationFn: () => getPublicLoyaltyStatus(tenantId, email),
    onSuccess: (data) => {
      setResult(data);
      setNotFound(false);
    },
    onError: (error) => {
      setResult(null);
      // Mesma mensagem generica pra e-mail invalido, programa desligado ou
      // cliente inexistente — anti-enumeracao, ver PublicGetLoyaltyStatusQueryHandler.
      if (error instanceof ApiError && error.status === 404) {
        setNotFound(true);
      } else {
        setNotFound(false);
      }
    },
  });

  return (
    <div className="flex flex-col gap-3">
      <form
        className="flex flex-col gap-2 sm:flex-row"
        onSubmit={(event) => {
          event.preventDefault();
          setResult(null);
          setNotFound(false);
          if (email.trim()) {
            mutation.mutate();
          }
        }}
      >
        <Input
          type="email"
          required
          placeholder="seu-email@exemplo.com"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          aria-label="Seu e-mail"
        />
        <Button type="submit" disabled={mutation.isPending} className={buttonRadiusClassName}>
          {mutation.isPending ? "Consultando..." : "Consultar"}
        </Button>
      </form>

      {result ? (
        <div className="flex items-center gap-3 rounded-lg border border-border p-3 text-sm">
          <Gift className="text-primary size-5 shrink-0" />
          <div>
            <p className="font-medium">
              {result.loyaltyPoints} / {result.loyaltyVisitsForReward} visitas
            </p>
            <p className="text-muted-foreground">Recompensa: {result.loyaltyRewardDescription}</p>
          </div>
        </div>
      ) : null}

      {notFound ? (
        <p className="text-muted-foreground text-sm">Nao encontramos pontos de fidelidade para esse e-mail.</p>
      ) : null}
    </div>
  );
}
