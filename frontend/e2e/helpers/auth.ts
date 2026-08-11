import { expect, type APIRequestContext, type Page } from "@playwright/test";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5071";
const PASSWORD = "SenhaForte123!";

/**
 * Cria um tenant + dono novos direto na API (rapido, deterministico) e faz
 * login pela UI de verdade — o access token so existe em memoria (nunca
 * localStorage/cookie legivel, ver session-context.tsx), entao nao ha atalho
 * de storageState: cada teste autenticado precisa passar pelo formulario de
 * login, exatamente como um usuario real.
 *
 * IMPORTANTE para quem escrever o proximo teste autenticado: depois do
 * login, navegue SEMPRE clicando em links (`page.getByRole("link", ...).click()`),
 * nunca com `page.goto(...)` — goto faz uma navegacao de pagina inteira, que
 * reinicia o JS e perde o access token em memoria, derrubando de volta pro
 * /login.
 */
export async function createTenantAndLogIn(page: Page, request: APIRequestContext): Promise<void> {
  const suffix = crypto.randomUUID().replace(/-/g, "").slice(0, 16);
  const slug = `e2e-${suffix}`;
  const email = `owner-${suffix}@example.com`;

  const tenantResponse = await request.post(`${API_BASE_URL}/api/tenants`, {
    data: { name: `E2E ${suffix}`, slug, businessType: "Other", timeZoneId: "America/Sao_Paulo" },
  });
  expect(tenantResponse.ok(), `Falha ao criar tenant de teste: ${tenantResponse.status()}`).toBeTruthy();

  const registerResponse = await request.post(`${API_BASE_URL}/api/auth/register`, {
    data: { tenantId: (await tenantResponse.json()).id, email, password: PASSWORD, fullName: "Dono E2E" },
  });
  expect(registerResponse.ok(), `Falha ao registrar dono de teste: ${registerResponse.status()}`).toBeTruthy();

  await page.goto("/login");
  await page.getByLabel("Identificador do estabelecimento").fill(slug);
  await page.getByLabel("E-mail").fill(email);
  await page.getByLabel("Senha").fill(PASSWORD);
  await page.getByRole("button", { name: "Entrar" }).click();

  await expect(page).toHaveURL(/\/painel$/);
}
