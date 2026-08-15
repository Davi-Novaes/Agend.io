"use client";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { daysAgo, startOfMonth, startOfWeek, startOfYear, toDateOnly } from "@/lib/date-utils";

export function PeriodFilter({
  from,
  to,
  onFromChange,
  onToChange,
}: {
  from: string;
  to: string;
  onFromChange: (value: string) => void;
  onToChange: (value: string) => void;
}) {
  function applyPreset(preset: "today" | "week" | "month" | "year" | "30" | "90") {
    if (preset === "today") {
      onFromChange(toDateOnly(new Date()));
    } else if (preset === "week") {
      onFromChange(toDateOnly(startOfWeek(new Date())));
    } else if (preset === "month") {
      onFromChange(toDateOnly(startOfMonth(new Date())));
    } else if (preset === "year") {
      onFromChange(toDateOnly(startOfYear(new Date())));
    } else if (preset === "30") {
      onFromChange(toDateOnly(daysAgo(30)));
    } else {
      onFromChange(toDateOnly(daysAgo(90)));
    }
    onToChange(toDateOnly(new Date()));
  }

  return (
    <div className="flex flex-wrap items-end gap-2">
      <div className="flex flex-wrap gap-1">
        <Button type="button" variant="outline" size="sm" onClick={() => applyPreset("today")}>
          Hoje
        </Button>
        <Button type="button" variant="outline" size="sm" onClick={() => applyPreset("week")}>
          Esta semana
        </Button>
        <Button type="button" variant="outline" size="sm" onClick={() => applyPreset("month")}>
          Este mes
        </Button>
        <Button type="button" variant="outline" size="sm" onClick={() => applyPreset("year")}>
          Este ano
        </Button>
        <Button type="button" variant="outline" size="sm" onClick={() => applyPreset("30")}>
          Ultimos 30 dias
        </Button>
        <Button type="button" variant="outline" size="sm" onClick={() => applyPreset("90")}>
          Ultimos 90 dias
        </Button>
      </div>
      <div className="flex items-end gap-2">
        <div>
          <label htmlFor="period-from-date" className="text-muted-foreground mb-1 block text-xs">
            De
          </label>
          <Input id="period-from-date" type="date" value={from} onChange={(event) => onFromChange(event.target.value)} />
        </div>
        <div>
          <label htmlFor="period-to-date" className="text-muted-foreground mb-1 block text-xs">
            Ate
          </label>
          <Input id="period-to-date" type="date" value={to} onChange={(event) => onToChange(event.target.value)} />
        </div>
      </div>
    </div>
  );
}
