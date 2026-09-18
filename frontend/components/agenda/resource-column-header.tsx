import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { resolveAssetUrl, type ResourceSummary } from "@/lib/api/client";

export type ResourceAvailability = "Ausente" | "EmAtendimento" | "Disponivel" | null;

const AVAILABILITY: Record<Exclude<ResourceAvailability, null>, { label: string; dot: string; text: string }> = {
  Disponivel: { label: "Disponivel", dot: "bg-success", text: "text-success" },
  EmAtendimento: { label: "Em atendimento", dot: "bg-warning", text: "text-warning" },
  Ausente: { label: "Ausente", dot: "bg-muted-foreground", text: "text-muted-foreground" },
};

function initials(name: string): string {
  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("");
}

export function ResourceColumnHeader({
  resource,
  availability,
  appointmentCount,
  utilizationPercent,
}: {
  resource: ResourceSummary;
  availability: ResourceAvailability;
  appointmentCount: number;
  utilizationPercent: number | null;
}) {
  const status = availability ? AVAILABILITY[availability] : null;

  return (
    <div className="flex items-center gap-2.5 px-3 py-2.5">
      <Avatar size="lg" className="shrink-0">
        {resource.photoUrl && <AvatarImage src={resolveAssetUrl(resource.photoUrl)} alt="" />}
        <AvatarFallback className="text-xs">{initials(resource.name)}</AvatarFallback>
      </Avatar>

      <div className="min-w-0 flex-1">
        <div className="flex items-center justify-between gap-2">
          <p className="truncate text-sm font-semibold">{resource.name}</p>
          {status && (
            <span className={`flex shrink-0 items-center gap-1 text-[10px] font-medium ${status.text}`}>
              <span className={`size-1.5 rounded-full ${status.dot}`} aria-hidden="true" />
              {status.label}
            </span>
          )}
        </div>
        <p className="text-muted-foreground mt-0.5 text-[11px]">
          {appointmentCount === 1 ? "1 agendamento" : `${appointmentCount} agendamentos`}
        </p>
        {utilizationPercent !== null && (
          <div className="mt-1.5 flex items-center gap-2">
            <div className="bg-muted h-1 flex-1 overflow-hidden rounded-full">
              <div
                className="bg-primary h-full rounded-full transition-all"
                style={{ width: `${Math.min(100, Math.round(utilizationPercent))}%` }}
              />
            </div>
            <span className="text-muted-foreground shrink-0 text-[10px] tabular-nums">{Math.round(utilizationPercent)}%</span>
          </div>
        )}
      </div>
    </div>
  );
}
