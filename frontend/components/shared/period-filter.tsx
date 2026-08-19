"use client";

import * as React from "react";
import { CalendarRange, Check } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import {
  daysAgo,
  endOfPreviousMonth,
  startOfMonth,
  startOfPreviousMonth,
  startOfWeek,
  startOfYear,
  toDateOnly,
} from "@/lib/date-utils";

type PresetKey = "today" | "yesterday" | "week" | "month" | "lastMonth" | "year" | "30" | "90" | "custom";

const PRESET_LABELS: Record<PresetKey, string> = {
  today: "Hoje",
  yesterday: "Ontem",
  week: "Esta semana",
  month: "Este mes",
  lastMonth: "Mes passado",
  year: "Este ano",
  "30": "Ultimos 30 dias",
  "90": "Ultimos 90 dias",
  custom: "Personalizado",
};

const ORDERED_PRESETS: Exclude<PresetKey, "custom">[] = ["today", "yesterday", "week", "month", "lastMonth", "year", "30", "90"];

function presetRange(preset: Exclude<PresetKey, "custom">): { from: string; to: string } {
  const today = new Date();
  switch (preset) {
    case "today":
      return { from: toDateOnly(today), to: toDateOnly(today) };
    case "yesterday": {
      const yesterday = daysAgo(1);
      return { from: toDateOnly(yesterday), to: toDateOnly(yesterday) };
    }
    case "week":
      return { from: toDateOnly(startOfWeek(today)), to: toDateOnly(today) };
    case "month":
      return { from: toDateOnly(startOfMonth(today)), to: toDateOnly(today) };
    case "lastMonth":
      return { from: toDateOnly(startOfPreviousMonth(today)), to: toDateOnly(endOfPreviousMonth(today)) };
    case "year":
      return { from: toDateOnly(startOfYear(today)), to: toDateOnly(today) };
    case "30":
      return { from: toDateOnly(daysAgo(30)), to: toDateOnly(today) };
    case "90":
      return { from: toDateOnly(daysAgo(90)), to: toDateOnly(today) };
  }
}

function activePreset(from: string, to: string): PresetKey {
  const match = ORDERED_PRESETS.find((preset) => {
    const range = presetRange(preset);
    return range.from === from && range.to === to;
  });
  return match ?? "custom";
}

function formatShortDate(value: string): string {
  const [year, month, day] = value.split("-").map(Number);
  return new Date(year, month - 1, day).toLocaleDateString("pt-BR", { day: "2-digit", month: "short" });
}

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
  const [open, setOpen] = React.useState(false);
  const [showCustom, setShowCustom] = React.useState(false);
  const preset = activePreset(from, to);

  function handleOpenChange(next: boolean) {
    setOpen(next);
    if (next) {
      // Reabrir sempre reflete o estado real de from/to — se from/to nao bate
      // com nenhum preset (ex.: veio de fora), comeca ja mostrando os campos.
      setShowCustom(preset === "custom");
    }
  }

  function applyPreset(key: Exclude<PresetKey, "custom">) {
    const range = presetRange(key);
    onFromChange(range.from);
    onToChange(range.to);
    setShowCustom(false);
    setOpen(false);
  }

  return (
    <Popover open={open} onOpenChange={handleOpenChange}>
      <PopoverTrigger asChild>
        <Button type="button" variant="outline" size="sm" className="gap-2">
          <CalendarRange className="size-4 text-muted-foreground" aria-hidden="true" />
          <span>{PRESET_LABELS[preset]}</span>
          <span className="text-muted-foreground">
            · {formatShortDate(from)} – {formatShortDate(to)}
          </span>
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-64">
        <div className="flex flex-col gap-0.5">
          {ORDERED_PRESETS.map((key) => (
            <button
              key={key}
              type="button"
              onClick={() => applyPreset(key)}
              className="flex items-center justify-between rounded-md px-2 py-1.5 text-left text-sm hover:bg-accent hover:text-accent-foreground"
            >
              {PRESET_LABELS[key]}
              {preset === key && <Check className="size-4" aria-hidden="true" />}
            </button>
          ))}
          <button
            type="button"
            onClick={() => setShowCustom(true)}
            aria-pressed={showCustom}
            className="flex items-center justify-between rounded-md px-2 py-1.5 text-left text-sm hover:bg-accent hover:text-accent-foreground"
          >
            {PRESET_LABELS.custom}
            {(showCustom || preset === "custom") && <Check className="size-4" aria-hidden="true" />}
          </button>
        </div>

        {showCustom && (
          <div className="mt-3 flex items-end gap-2 border-t pt-3">
            <div>
              <label htmlFor="period-from-date" className="mb-1 block text-xs text-muted-foreground">
                De
              </label>
              <Input id="period-from-date" type="date" value={from} onChange={(event) => onFromChange(event.target.value)} />
            </div>
            <div>
              <label htmlFor="period-to-date" className="mb-1 block text-xs text-muted-foreground">
                Ate
              </label>
              <Input id="period-to-date" type="date" value={to} onChange={(event) => onToChange(event.target.value)} />
            </div>
          </div>
        )}
      </PopoverContent>
    </Popover>
  );
}
