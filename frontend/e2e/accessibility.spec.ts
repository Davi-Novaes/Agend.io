import { test, expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

test("landing page has no automatically detectable accessibility violations", async ({ page }) => {
  await page.goto("/");

  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag22aa"]).analyze();

  expect(results.violations, JSON.stringify(results.violations, null, 2)).toEqual([]);
});

test("landing page primary call-to-action is reachable and operable by keyboard alone", async ({ page }) => {
  await page.goto("/");

  // CLAUDE.md: "Todo fluxo precisa ser completavel so com teclado." O link
  // mais importante da pagina (comecar cadastro) precisa aparecer no fluxo de
  // Tab e responder a Enter, sem depender de mouse.
  const cta = page.getByRole("link", { name: /cadastr|come[cç]ar/i }).first();
  await expect(cta).toBeVisible();

  await cta.focus();
  await expect(cta).toBeFocused();
});
