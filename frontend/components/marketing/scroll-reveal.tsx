"use client";

import * as React from "react";
import { cn } from "@/lib/utils";

type Variant = "up" | "scale" | "fade";

const VARIANT_CLASSES: Record<Variant, string> = {
  up: "slide-in-from-bottom-6",
  scale: "zoom-in-95",
  fade: "",
};

/**
 * Revela o filho com animate-in (tw-animate-css, ja usado em todo o design
 * system para Popover/Dialog/Sheet) na primeira vez que entra na viewport --
 * so entao para de observar, entao nao ha custo continuo de scroll. Sem
 * prefers-reduced-motion, o filho so aparece direto (skipAnimation), como
 * pede o CLAUDE.md (WCAG 2.2 AA).
 */
export function ScrollReveal({
  children,
  variant = "up",
  delayMs = 0,
  className,
}: {
  children: React.ReactNode;
  variant?: Variant;
  delayMs?: number;
  className?: string;
}) {
  const ref = React.useRef<HTMLDivElement>(null);
  const [visible, setVisible] = React.useState(
    () => typeof window !== "undefined" && window.matchMedia("(prefers-reduced-motion: reduce)").matches
  );

  React.useEffect(() => {
    if (visible) return;
    const node = ref.current;
    if (!node) return;

    // Se o elemento ja chegou visivel no primeiro paint (link com #ancora,
    // foco por teclado pulando direto pra ele, ou restauracao de scroll do
    // navegador ao recarregar a pagina no meio do FAQ), o IntersectionObserver
    // NAO dispara callback nenhuma -- ele so reporta MUDANCAS de intersecao a
    // partir de agora, e o estado atual (ja intersectando) nao conta como
    // mudanca. Sem este cheque sincrono, o elemento ficava preso em
    // opacity-0 pra sempre (bug real, visto no FAQ da home: secao inteira
    // some quando a rolagem chega nela de forma instantanea em vez de suave).
    const rect = node.getBoundingClientRect();
    const alreadyVisible = rect.top < window.innerHeight * 0.9 && rect.bottom > 0;
    if (alreadyVisible) {
      setVisible(true);
      return;
    }

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setVisible(true);
          observer.disconnect();
        }
      },
      { threshold: 0.15, rootMargin: "0px 0px -10% 0px" }
    );
    observer.observe(node);
    return () => observer.disconnect();
  }, [visible]);

  return (
    <div
      ref={ref}
      style={visible ? { animationDelay: `${delayMs}ms` } : undefined}
      className={cn(
        visible ? cn("animate-in fade-in-0 fill-mode-both duration-700 ease-out", VARIANT_CLASSES[variant]) : "opacity-0",
        className
      )}
    >
      {children}
    </div>
  );
}
