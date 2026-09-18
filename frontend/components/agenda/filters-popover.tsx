import { ListFilter, X } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Separator } from "@/components/ui/separator";
import { APPOINTMENT_STATUS_LABELS } from "@/lib/appointment-status";
import type { AppointmentStatus, ResourceSummary, ServiceSummary } from "@/lib/api/client";

const STATUS_OPTIONS = Object.keys(APPOINTMENT_STATUS_LABELS) as AppointmentStatus[];

export type AgendaFilters = {
  statuses: Set<AppointmentStatus>;
  serviceIds: Set<string>;
  resourceIds: Set<string>;
};

export function isFiltersEmpty(filters: AgendaFilters): boolean {
  return filters.statuses.size === 0 && filters.serviceIds.size === 0 && filters.resourceIds.size === 0;
}

function toggle<T>(set: Set<T>, value: T): Set<T> {
  const next = new Set(set);
  if (next.has(value)) {
    next.delete(value);
  } else {
    next.add(value);
  }
  return next;
}

export function AgendaFiltersPopover({
  filters,
  onChange,
  services,
  resources,
}: {
  filters: AgendaFilters;
  onChange: (filters: AgendaFilters) => void;
  services: ServiceSummary[];
  resources: ResourceSummary[];
}) {
  const activeCount = filters.statuses.size + filters.serviceIds.size + filters.resourceIds.size;

  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button variant="outline" size="sm">
          <ListFilter className="size-4" />
          Filtros
          {activeCount > 0 && (
            <Badge variant="secondary" className="ml-1">
              {activeCount}
            </Badge>
          )}
        </Button>
      </PopoverTrigger>
      <PopoverContent className="flex max-h-96 flex-col gap-3 overflow-y-auto" align="end">
        <div>
          <p className="mb-1.5 text-xs font-medium">Status</p>
          <div className="flex flex-col gap-1.5">
            {STATUS_OPTIONS.map((status) => (
              <label key={status} className="flex items-center gap-2 text-sm">
                <Checkbox
                  checked={filters.statuses.has(status)}
                  onCheckedChange={() => onChange({ ...filters, statuses: toggle(filters.statuses, status) })}
                />
                {APPOINTMENT_STATUS_LABELS[status]}
              </label>
            ))}
          </div>
        </div>

        {services.length > 0 && (
          <>
            <Separator />
            <div>
              <p className="mb-1.5 text-xs font-medium">Servico</p>
              <div className="flex flex-col gap-1.5">
                {services.map((service) => (
                  <label key={service.id} className="flex items-center gap-2 text-sm">
                    <Checkbox
                      checked={filters.serviceIds.has(service.id)}
                      onCheckedChange={() => onChange({ ...filters, serviceIds: toggle(filters.serviceIds, service.id) })}
                    />
                    {service.name}
                  </label>
                ))}
              </div>
            </div>
          </>
        )}

        {resources.length > 0 && (
          <>
            <Separator />
            <div>
              <p className="mb-1.5 text-xs font-medium">Profissional</p>
              <div className="flex flex-col gap-1.5">
                {resources.map((resource) => (
                  <label key={resource.id} className="flex items-center gap-2 text-sm">
                    <Checkbox
                      checked={filters.resourceIds.has(resource.id)}
                      onCheckedChange={() => onChange({ ...filters, resourceIds: toggle(filters.resourceIds, resource.id) })}
                    />
                    {resource.name}
                  </label>
                ))}
              </div>
            </div>
          </>
        )}

        {activeCount > 0 && (
          <Button
            variant="ghost"
            size="sm"
            className="self-start"
            onClick={() => onChange({ statuses: new Set(), serviceIds: new Set(), resourceIds: new Set() })}
          >
            <X className="size-3.5" />
            Limpar filtros
          </Button>
        )}
      </PopoverContent>
    </Popover>
  );
}
