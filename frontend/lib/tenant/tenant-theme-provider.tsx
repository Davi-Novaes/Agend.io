"use client";

import * as React from "react";
import { DEFAULT_TENANT_THEME, type TenantTheme } from "@/lib/tenant/tenant-theme";

// Aplicado no <html> via variavel CSS, nao troca de classe/tema inteiro: um
// tenant so redefine --primary/--primary-foreground, o resto da paleta base
// (light/dark do shadcn) continua igual. Isolar a mudanca a duas variaveis
// evita que a personalizacao do tenant quebre o contraste calculado pelo
// resto do design system.
export function TenantThemeProvider({
  theme = DEFAULT_TENANT_THEME,
  children,
}: {
  theme?: TenantTheme;
  children: React.ReactNode;
}) {
  React.useEffect(() => {
    const root = document.documentElement;
    root.style.setProperty("--primary", theme.primary);
    root.style.setProperty("--primary-foreground", theme.primaryForeground);

    return () => {
      root.style.removeProperty("--primary");
      root.style.removeProperty("--primary-foreground");
    };
  }, [theme]);

  return children;
}
