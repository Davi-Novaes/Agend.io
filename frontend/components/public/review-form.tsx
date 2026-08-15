"use client";

import * as React from "react";
import { useMutation } from "@tanstack/react-query";

import { submitReview, ApiError } from "@/lib/api/client";
import { StarRating } from "@/components/scheduling/star-rating";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";

export function ReviewForm({
  tenantId,
  appointmentId,
  buttonRadiusClassName,
}: {
  tenantId: string;
  appointmentId: string;
  buttonRadiusClassName?: string;
}) {
  const [email, setEmail] = React.useState("");
  const [rating, setRating] = React.useState(0);
  const [comment, setComment] = React.useState("");

  const mutation = useMutation({
    mutationFn: () => submitReview(tenantId, appointmentId, { customerEmail: email, rating, comment: comment.trim() || null }),
  });

  if (mutation.isSuccess) {
    return (
      <p className="text-center text-sm">Obrigado pela sua avaliacao!</p>
    );
  }

  return (
    <form
      className="flex flex-col gap-4"
      onSubmit={(event) => {
        event.preventDefault();
        if (rating > 0 && email.trim()) {
          mutation.mutate();
        }
      }}
    >
      <div className="flex flex-col items-center gap-2">
        <StarRating value={rating} onChange={setRating} size="lg" />
        <p className="text-muted-foreground text-xs">Toque nas estrelas para avaliar</p>
      </div>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-medium" htmlFor="review-email">
          Seu e-mail
        </label>
        <Input
          id="review-email"
          type="email"
          required
          placeholder="seu-email@exemplo.com"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
        />
        <p className="text-muted-foreground text-xs">Usamos seu e-mail so para confirmar que a avaliacao e sua.</p>
      </div>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-medium" htmlFor="review-comment">
          Comentario (opcional)
        </label>
        <Textarea
          id="review-comment"
          rows={4}
          value={comment}
          onChange={(event) => setComment(event.target.value)}
        />
      </div>

      {mutation.isError ? (
        <p className="text-destructive text-sm">
          {mutation.error instanceof ApiError ? mutation.error.message : "Nao foi possivel enviar sua avaliacao."}
        </p>
      ) : null}

      <Button type="submit" disabled={mutation.isPending || rating === 0 || !email.trim()} className={buttonRadiusClassName}>
        {mutation.isPending ? "Enviando..." : "Enviar avaliacao"}
      </Button>
    </form>
  );
}
