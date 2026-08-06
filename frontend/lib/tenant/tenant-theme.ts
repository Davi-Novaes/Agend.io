// Paleta minima de um tenant. A busca real (tenant settings, Sprint 1) so
// precisa preencher este tipo — o provider ja sabe aplicar.
export type TenantTheme = {
  primary: string;
  primaryForeground: string;
};

// Reaproveita o --primary/--primary-foreground padrao do shadcn (ver
// app/globals.css) ate a empresa configurar uma paleta propria.
export const DEFAULT_TENANT_THEME: TenantTheme = {
  primary: "oklch(0.205 0 0)",
  primaryForeground: "oklch(0.985 0 0)",
};
