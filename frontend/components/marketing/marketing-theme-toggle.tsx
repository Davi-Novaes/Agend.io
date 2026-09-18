"use client";

import { Moon, Sun, Monitor } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useScopedTheme } from "@/lib/theme/scoped-theme";

const OPTIONS = [
  { value: "light", label: "Claro", icon: Sun },
  { value: "dark", label: "Escuro", icon: Moon },
  { value: "system", label: "Sistema", icon: Monitor },
] as const;

// Mesmo visual de components/theme-toggle.tsx, mas ligado ao
// ScopedThemeProvider da area de marketing (storageKey proprio) em vez do
// next-themes global -- trocar aqui nunca afeta o tema do painel autenticado.
export function MarketingThemeToggle() {
  const { preference, setPreference } = useScopedTheme();

  const current = OPTIONS.find((option) => option.value === preference) ?? OPTIONS[2];
  const Icon = current.icon;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" aria-label="Alternar tema">
          <Icon className="size-4" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        {OPTIONS.map((option) => (
          <DropdownMenuItem key={option.value} onClick={() => setPreference(option.value)}>
            <option.icon className="size-4" />
            {option.label}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
