"use client";

import * as React from "react";
import { Star } from "lucide-react";
import { cn } from "@/lib/utils";

export function StarRating({
  value,
  onChange,
  readOnly = false,
  size = "md",
}: {
  value: number;
  onChange?: (rating: number) => void;
  readOnly?: boolean;
  size?: "sm" | "md" | "lg";
}) {
  const [hovered, setHovered] = React.useState<number | null>(null);
  const displayValue = hovered ?? value;
  const sizeClass = size === "sm" ? "size-4" : size === "lg" ? "size-8" : "size-6";

  if (readOnly) {
    return (
      <div className="flex items-center gap-0.5" role="img" aria-label={`${value.toFixed(1)} de 5 estrelas`}>
        {[1, 2, 3, 4, 5].map((star) => (
          <Star
            key={star}
            className={cn(sizeClass, star <= Math.round(value) ? "fill-warning text-warning" : "text-muted-foreground/30")}
          />
        ))}
      </div>
    );
  }

  return (
    <div className="flex items-center gap-1" role="radiogroup" aria-label="Sua avaliacao">
      {[1, 2, 3, 4, 5].map((star) => (
        <button
          key={star}
          type="button"
          role="radio"
          aria-checked={value === star}
          aria-label={`${star} ${star === 1 ? "estrela" : "estrelas"}`}
          className="rounded-sm focus-visible:ring-ring focus-visible:ring-2 focus-visible:outline-none"
          onMouseEnter={() => setHovered(star)}
          onMouseLeave={() => setHovered(null)}
          onClick={() => onChange?.(star)}
        >
          <Star className={cn(sizeClass, star <= displayValue ? "fill-warning text-warning" : "text-muted-foreground/30")} />
        </button>
      ))}
    </div>
  );
}
