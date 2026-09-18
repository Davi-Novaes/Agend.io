"use client";

import * as React from "react";

import { cn } from "@/lib/utils";

/**
 * Luz que segue o cursor do mouse -- usado em telas de fundo escuro com o
 * glow ambiente (Hero da home, onboarding). Escuta mousemove no window (nao
 * precisa que o pai passe handlers, o que importa porque o pai as vezes e um
 * Server Component e nao pode ter onMouseMove na propria JSX) e so fica ativo
 * enquanto o cursor esta dentro do proprio container (getBoundingClientRect).
 * So um efeito de hover direto (a pessoa move o proprio mouse), nao uma
 * animacao automatica -- por isso nao e desligado em prefers-reduced-motion,
 * e some sozinho em telas sem mouse de verdade (guarda com "hover: hover").
 */
export function CursorSpotlight({ size }: { size?: number } = {}) {
  const containerRef = React.useRef<HTMLDivElement>(null);
  const spotlightRef = React.useRef<HTMLDivElement>(null);
  const [isActive, setIsActive] = React.useState(false);

  React.useEffect(() => {
    if (!window.matchMedia("(hover: hover)").matches) return;

    function handleMouseMove(event: MouseEvent) {
      const container = containerRef.current;
      const spotlight = spotlightRef.current;
      if (!container || !spotlight) return;

      const rect = container.getBoundingClientRect();
      const withinBounds =
        event.clientX >= rect.left && event.clientX <= rect.right && event.clientY >= rect.top && event.clientY <= rect.bottom;

      setIsActive(withinBounds);
      if (withinBounds) {
        spotlight.style.setProperty("--spotlight-x", `${event.clientX - rect.left}px`);
        spotlight.style.setProperty("--spotlight-y", `${event.clientY - rect.top}px`);
      }
    }

    window.addEventListener("mousemove", handleMouseMove);
    return () => window.removeEventListener("mousemove", handleMouseMove);
  }, []);

  return (
    <div ref={containerRef} aria-hidden className="pointer-events-none absolute inset-0">
      <div
        ref={spotlightRef}
        className={cn("cursor-spotlight", isActive && "is-active")}
        style={size ? ({ "--spotlight-size": `${size}px` } as React.CSSProperties) : undefined}
      />
    </div>
  );
}
