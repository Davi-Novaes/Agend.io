import { defineConfig, devices } from "@playwright/test";

// Gate minimo de acessibilidade exigido pelo CLAUDE.md ("axe-core roda no
// Playwright como gate de CI"). Cobre so a pagina publica mais critica (landing
// e portal do cliente) de proposito — uma suite E2E completa fica para depois,
// o objetivo aqui e ter ALGUM gate real em vez de zero, que era o estado antes
// do Sprint 6.
export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? "github" : "list",
  use: {
    baseURL: "http://localhost:3000",
    trace: "on-first-retry",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: {
    command: "npm run start",
    url: "http://localhost:3000",
    reuseExistingServer: !process.env.CI,
    timeout: 60_000,
  },
});
