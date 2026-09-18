"use client";

import * as React from "react";

/** Facilita in-out, evita a contagem terminar abrupta -- so estetica. */
function easeOutQuad(t: number): number {
  return 1 - (1 - t) * (1 - t);
}

/**
 * Conta de 0 ate `value` via requestAnimationFrame (nao setInterval, pra nao
 * disparar fora do frame de renderizacao) na primeira vez que entra na
 * viewport. Com prefers-reduced-motion, mostra o valor final direto.
 */
export function AnimatedCounter({
  value,
  prefix = "",
  suffix = "",
  durationMs = 1400,
}: {
  value: number;
  prefix?: string;
  suffix?: string;
  durationMs?: number;
}) {
  const ref = React.useRef<HTMLSpanElement>(null);
  const [prefersReducedMotion] = React.useState(
    () => typeof window !== "undefined" && window.matchMedia("(prefers-reduced-motion: reduce)").matches
  );
  const [display, setDisplay] = React.useState(() => (prefersReducedMotion ? value : 0));

  React.useEffect(() => {
    if (prefersReducedMotion) return;
    const node = ref.current;
    if (!node) return;

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (!entry.isIntersecting) return;
        const startTime = performance.now();
        function tick(now: number) {
          const progress = Math.min(1, (now - startTime) / durationMs);
          setDisplay(Math.round(value * easeOutQuad(progress)));
          if (progress < 1) requestAnimationFrame(tick);
        }
        requestAnimationFrame(tick);
        observer.disconnect();
      },
      { threshold: 0.4 }
    );
    observer.observe(node);
    return () => observer.disconnect();
  }, [value, durationMs, prefersReducedMotion]);

  return (
    <span ref={ref} className="tabular-nums">
      {prefix}
      {display.toLocaleString("pt-BR")}
      {suffix}
    </span>
  );
}
