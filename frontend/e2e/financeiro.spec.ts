import { test, expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

import { createTenantAndLogIn } from "./helpers/auth";

// Primeiro gate de acessibilidade autenticado do projeto (ate aqui so a
// landing publica tinha axe-core — ver accessibility.spec.ts). /financeiro
// escolhida por ja ter tabs, tabela, dialog e grafico no mesmo lugar.
test("financeiro page has no automatically detectable accessibility violations", async ({ page, request }) => {
  await createTenantAndLogIn(page, request);

  // Navegacao client-side (nao page.goto): o access token so existe em
  // memoria (ver session-context.tsx) — uma navegacao de pagina inteira
  // perderia a sessao e cairia de volta no /login.
  await page.getByRole("link", { name: "Financeiro" }).click();
  await expect(page.getByRole("heading", { name: "Financeiro" })).toBeVisible();

  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag22aa"]).analyze();

  expect(results.violations, JSON.stringify(results.violations, null, 2)).toEqual([]);
});
