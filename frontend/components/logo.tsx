import { CalendarCheck2 } from "lucide-react";
import { cn } from "@/lib/utils";

export function Logo({
  className,
  inverted = false,
}: {
  className?: string;
  inverted?: boolean;
}) {
  return (
    <span className={cn("inline-flex items-center gap-2 font-semibold tracking-tight", className)}>
      <span
        className={cn(
          "flex size-8 items-center justify-center rounded-lg",
          inverted ? "bg-primary-foreground/15" : "bg-primary text-primary-foreground"
        )}
      >
        <CalendarCheck2 className="size-4.5" strokeWidth={2.25} />
      </span>
      Agendio
    </span>
  );
}
