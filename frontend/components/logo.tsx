import Image from "next/image";
import { cn } from "@/lib/utils";

export function Logo({
  className,
  inverted = false,
  tagline = true,
}: {
  className?: string;
  inverted?: boolean;
  /** "Controle de Estabelecimento" embaixo do nome -- desligado so onde o espaco e realmente apertado (ver AppSidebar). */
  tagline?: boolean;
}) {
  return (
    <span className={cn("inline-flex items-center gap-2 font-semibold tracking-tight", className)}>
      <span
        className={cn(
          "flex size-8 shrink-0 items-center justify-center rounded-lg",
          // O mark (logo.png) e um degrade roxo -- em cima do proprio
          // bg-primary roxo (ver LoginShowcase) precisa de um card claro atras
          // pra nao sumir por falta de contraste.
          inverted && "bg-primary-foreground p-1"
        )}
      >
        {/* unoptimized: o pipeline de otimizacao do next/image (reencode pra
            AVIF/WebP em qualidade reduzida) estava achatando a transparencia
            do PNG pra um cinza solido -- confirmado lendo os pixels reais
            desenhados no navegador. Arquivo ja e pequeno o bastante pra nao
            precisar de otimizacao nenhuma. */}
        <Image src="/logo.png" alt="" width={32} height={32} className="size-full object-contain" unoptimized priority />
      </span>
      {/* flex-col: tagline sempre alinhada embaixo do "agendiobr", nunca do
          icone -- os dois dividem a mesma coluna. */}
      <span className="flex flex-col leading-tight">
        <span>
          agendio
          {/* text-primary solido (nao degrade): --primary ja e a cor usada
              em botao/link no app inteiro, ou seja ja e garantidamente
              legivel tanto no tema claro quanto no escuro -- um degrade
              fixo (ver historico deste arquivo) tinha uma ponta escura
              demais e sumia em fundo escuro. */}
          <span className={inverted ? "text-primary-foreground/70" : "text-primary"}>br</span>
        </span>
        {tagline && (
          <span className={cn("text-[0.65rem] font-normal tracking-wide", inverted ? "text-primary-foreground/60" : "text-muted-foreground")}>
            Controle de Estabelecimento
          </span>
        )}
      </span>
    </span>
  );
}
