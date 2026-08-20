# Auditoria de Backend — Agendio

**Data:** 2026-08-18/19
**Escopo:** `.NET 10` modular monolith, 12 módulos, PostgreSQL com Row Level Security, Redis, RabbitMQ, Hangfire.
**Método:** revisão de código módulo a módulo + testes reais contra a API rodando em `http://localhost:5071` (nenhum teste foi assumido — todo resultado abaixo tem request/response real).

## Resumo executivo

| Severidade | Quantidade |
|---|---|
| P0 (crítico) | 1 |
| P1 (alto) | 2 |
| P2 (médio) | 2 |
| P3 (baixo) | 1 |

**Isolamento entre tenants (o que a tarefa pediu para testar primeiro): passou em todas as tentativas.** Nenhum dado do Tenant A vazou para o Tenant B em nenhuma leitura, listagem ou escrita testada — todas as tentativas de acesso cruzado devolveram `404` (leitura/escrita por Id) ou coleção vazia (listagem), nunca dado de outro tenant. A suíte automatizada de isolamento (`Tenant`/`CrossTenant`, 73 testes) também passa.

O achado P0 **não é** vazamento entre tenants — é uma falha de autorização diferente: um endpoint de billing aceita mutação de estado (ativação de assinatura) de **qualquer** tenant sem exigir nenhuma autenticação, e isso foi explorado de ponta a ponta neste teste (ver P0-1).

---

## Tabela de achados

| ID | Descrição | Severidade | Arquivo/Endpoint | Evidência |
|---|---|---|---|---|
| P0-1 | `POST /api/billing/subscription/onboard-select-plan` é anônimo e ativa a assinatura de **qualquer** tenant a partir só do `tenantId` no corpo — sem exigir prova de posse do tenant. Explorado ao vivo: um `curl` sem nenhum header de autenticação ativou o plano Grátis do Tenant B. | **P0** | `backend/src/modules/Billing/Agendio.Modules.Billing/Endpoints/BillingEndpoints.cs` (rota `onboard-select-plan`/`onboard-status`); `OnboardSelectPlanCommandHandler.cs` | Seção "Teste 7" abaixo — request/response completos |
| P1-1 | Token JWT autenticado mas sem a claim `tenant_id` derruba qualquer handler de escrita com `InvalidOperationException` não tratada → `500` com stack trace completo **e os headers da própria requisição (incluindo o Bearer token) refletidos no corpo da resposta**. Não há `app.UseExceptionHandler`/`IExceptionHandler` registrado em `Program.cs`. Endpoints de leitura mascaram o mesmo problema devolvendo `200` com lista vazia em vez de `401`. | **P1** | `backend/src/shared/Agendio.Infrastructure/Multitenancy/HttpTenantContext.cs:43` (getter que lança); `Program.cs` (sem exception handler global) | Seção "Teste 6" abaixo |
| P1-2 | A Camada 1 de defesa em profundidade descrita no `CLAUDE.md` ("subdomínio/slug + claim `tenant_id` no JWT; divergência = 403") **não existe no código**. O próprio comentário em `HttpTenantContext.cs` cita um "TenantMiddleware (Agendio.Api)" que não existe em lugar nenhum do repositório — `grep -r "TenantMiddleware"` só encontra a própria linha do comentário. `Program.cs` não tem nenhum passo de resolução de tenant por rota/subdomínio no pipeline. | **P1** | `backend/src/shared/Agendio.Infrastructure/Multitenancy/HttpTenantContext.cs:18-20`; `backend/src/host/Agendio.Api/Program.cs` (pipeline completo, linhas 274-338) | Grep documentado na seção "Achados de revisão de código" |
| P2-1 | N+1 query: `GetCustomerRecoveryCandidatesQueryHandler` faz um `await customerLookup.FindByIdAsync(...)` por candidato dentro de um `foreach`, sem método de busca em lote disponível no contrato do módulo Customers. | **P2** | `backend/src/modules/Scheduling/Agendio.Modules.Scheduling/Application/GetCustomerRecoveryCandidates/GetCustomerRecoveryCandidatesQueryHandler.cs:76-88`; contrato sem batch: `backend/src/modules/Customers/Agendio.Modules.Customers.Contracts/ICustomerLookupService.cs` | Leitura de código |
| P2-2 | Dependência `SSH.NET 2025.1.0` (projeto de testes, via Testcontainers) tem vulnerabilidade de alta severidade conhecida (`GHSA-q939-rpr3-3284`), sinalizada pelo próprio `dotnet build`/`dotnet test` (`NU1903`). Não é código de produção (não entra no binário do `Agendio.Api`), mas deveria ser atualizada. | **P2** | `backend/tests/Agendio.IntegrationTests/Agendio.IntegrationTests.csproj` | Output de `dotnet test` (warning `NU1903`) |
| P3-1 | E-mail e telefone de cliente aparecem em texto puro em alguns `LogInformation` (não é senha/token/CPF/dado de saúde — não viola a lista explícita do CLAUDE.md — mas é PII em log). | **P3** | `CampaignEmailJob.cs:31`, `CustomerMessageEmailJob.cs:26`, `CampaignWhatsAppJob.cs:34` (Marketing/Customers/Scheduling) | Grep de `LogInformation` |

### O que foi verificado e está correto (não é achado, mas vale registrar)

- **Testes de arquitetura:** `dotnet test backend/tests/Agendio.ArchitectureTests -c Release` → **31/31 aprovados**. As regras do CLAUDE.md (módulo não referencia módulo, Domain isolado, etc.) realmente são verificadas.
- **`IgnoreQueryFilters`:** apenas 4 usos em todo o `backend/src` (não 2 como a tarefa mencionava), todos em `Identity` (`AcceptInvitationCommandHandler`, `ConfirmEmailCommandHandler`, `LogoutCommandHandler`, `RefreshAccessTokenCommandHandler`) e todos **documentados** com comentário de classe explicando por que o tenant ainda não é conhecido naquele ponto (busca por hash de token de alta entropia, antes de `SetTenant` ser chamado). Nenhum uso órfão/sem justificativa encontrado.
- **Autorização de endpoint:** todos os 17 arquivos de endpoint foram lidos integralmente. Todo grupo autenticado usa `.RequireAuthorization()` no `MapGroup` (sem fallback policy global — checado em `Program.cs`, então cada grupo precisa declarar explicitamente, e todos declaram). Rotas `Owner`-only usam `.RequireAuthorization(policy => policy.RequireRole("Owner"))` de forma consistente (Financeiro, Tenancy, Identity/team). Rotas públicas (`AllowAnonymous`) têm comentário justificando o motivo em cada uma, exceto as duas do achado P0-1.
- **Dados sensíveis em log:** nenhuma ocorrência de senha, token JWT/refresh, CPF ou dado de saúde em `LogInformation`/`LogWarning`/`LogError` em código de produção. `LoggingBehavior` (pipeline global) só loga nome do tipo do comando/query, nunca o payload.
- **Segregação Platform vs Tenant:** dois schemes JWT completamente separados (issuer/audience/chave), confirmado lendo `Program.cs` — um token de tenant não valida na policy `PlatformOnly` e vice-versa.

---

## Teste de isolamento multi-tenant — passo a passo

### Setup

1. **Tenant A** criado via `POST /api/tenants` → `id: 32767c94-c191-4086-83b5-69d0d3d7f281` (slug `tenant-a-audit`).
2. **Tenant B** criado via `POST /api/tenants` → `id: 5bdd53a0-37f8-4e11-a4d7-a6907adc829a` (slug `tenant-b-audit`).
3. Owner registrado em cada tenant via `POST /api/auth/register`; e-mail confirmado direto no banco:
   ```
   docker exec -i agendio-postgres psql -U agendio_owner -d agendio -c \
     "UPDATE identity.users SET email_confirmed_at = now() WHERE email ILIKE '%auditagendio.test%';"
   -- UPDATE 2
   ```
4. Login de cada owner via `POST /api/auth/login` → `200`, access token A e B capturados.
5. No **Tenant A**, criados (todos `201 Created`, tokens salvos):
   - Serviço A: `c246943f-8736-4700-9751-f7b0b4069f26`
   - Recurso A (profissional): `d7420d3c-177b-4dae-85d3-a2f697ea9db0` (com working hours setados)
   - Cliente A: `33cc9d0e-b008-46a0-a824-a5ab88ec2e83` (com CPF e `healthNotes` preenchidos, de propósito, para confirmar que dado sensível também não vaza)
   - Agendamento A: `6a350654-f41a-491e-b809-89cff532d6fe`
   - Conta a pagar A: `36389e57-ad20-471d-8504-43a1bb65babb`

### Teste 1 — leitura direta por Id, token B contra recursos do Tenant A

| Tentativa | Esperado | Resultado real |
|---|---|---|
| `GET /api/customers/{customerA}` com token B | 404 | **404** `Customer.NotFound` |
| `GET /api/resources/{resourceA}` com token B | 404 | **404** `Resource.NotFound` |
| `GET /api/services/{serviceA}` com token B | 404 | **404** `Service.NotFound` |
| `GET /api/appointments/{appointmentA}` com token B | 404 | **404** `Appointment.NotFound` |
| `GET /api/financeiro/contas-a-pagar` (listagem, token B) | vazia | **`{"items":[],"totalCount":0,...}`** |

Nenhum dado do Tenant A (inclusive CPF/observação de saúde do cliente) apareceu na resposta do Tenant B em nenhum caso.

### Teste 2 — escrita (PUT/PATCH/POST) direta por Id, token B contra recursos do Tenant A

| Tentativa | Esperado | Resultado real |
|---|---|---|
| `PUT /api/customers/{customerA}` (`fullName: "HACKED BY TENANT B"`) | 404 | **404** `Customer.NotFound` |
| `PATCH /api/customers/{customerA}/status` (desativar) | 404 | **404** `Customer.NotFound` |
| `POST /api/appointments/{appointmentA}/cancel` | 404 | **404** `Appointment.NotFound` |
| `PATCH /api/financeiro/contas-a-pagar/{payableA}/pagar` | 404 | **404** `AccountPayable.NotFound` |
| `PATCH /api/resources/{resourceA}/status` (desativar) | 404 | **404** `Resource.NotFound` |
| `PUT /api/services/{serviceA}` (`name: "HACKED SERVICE"`) | 404 | **404** `Service.NotFound` |

Nenhuma escrita foi aceita — em todos os casos o Global Query Filter (camada 2) já devolve "não encontrado" antes de qualquer tentativa de UPDATE chegar ao banco.

### Teste 3 — listagens do Tenant B não vazam nada do Tenant A

```
GET /api/customers?page=1&pageSize=50      (token B) -> {"items":[],"totalCount":0,...}
GET /api/appointments?from=...&to=...      (token B) -> []   (janela cobre a data do agendamento A)
GET /api/services?page=1&pageSize=50       (token B) -> {"items":[],"totalCount":0,...}
GET /api/resources?page=1&pageSize=50      (token B) -> {"items":[],"totalCount":0,...}
```

### Teste 4 — token ausente/inválido

```
GET /api/customers  sem header Authorization        -> 401
GET /api/customers  com "Authorization: Bearer garbage.invalid.token" -> 401
GET /api/customers  com "Authorization: Bearer "     -> 401
```

### Teste 5 — token expirado e token com assinatura forjada

Gerados manualmente (HS256, mesma chave de dev de `appsettings.Development.json`, e uma chave errada) para simular:

- **Token expirado** (mesma assinatura válida, `exp` 30 min no passado, claims de Tenant A completas): `GET /api/customers` → **401**.
- **Token com assinatura forjada** (chave diferente da configurada): `GET /api/customers` → **401**.

Ambos corretamente rejeitados pelo middleware de autenticação JWT antes de qualquer lógica de negócio rodar.

### Teste 6 — token autenticado mas **sem** claim `tenant_id` (achado P1-1)

Gerado um JWT válido (assinatura, issuer, audience corretos — mesma chave de dev) com claims de `nameidentifier`/`role`/e-mail, mas **sem** `tenant_id`:

```
GET /api/customers  com esse token -> 200 {"items":[],"totalCount":0,"page":1,"pageSize":20}

POST /api/customers com esse token, body {"fullName":"No Tenant Attempt"} -> 500

System.InvalidOperationException: Nao ha tenant na requisicao atual. Verifique
ITenantContext.HasTenant antes de acessar TenantId (rotas publicas, como o
portal do cliente antes do login, nao tem tenant no token).
   at Agendio.Infrastructure.Multitenancy.HttpTenantContext.get_TenantId() ...
   at Agendio.Modules.Customers.Application.CreateCustomer.CreateCustomerCommandHandler.Handle(...) ...
   [stack trace completo com caminhos de arquivo e números de linha]

HEADERS
=======
Accept: */*
Host: localhost:5071
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...   <- o próprio Bearer token do chamador, refletido de volta
Content-Type: application/json
Content-Length: 32
```

Isso não vaza dado de **outro** tenant (o chamador só vê o próprio token refletido), mas: (a) é uma exceção não tratada em produção de qualquer handler que toque `tenantContext.TenantId` sem checar `HasTenant` antes; (b) a página de exceção do ASP.NET Core que produziu esse corpo de resposta só está ativa porque `ASPNETCORE_ENVIRONMENT=Development` — uma única configuração errada em produção reabriria esse vazamento de stack trace/estrutura interna para qualquer request que disparasse qualquer exceção não tratada, não só esta; (c) o comportamento é inconsistente entre leitura (mascara com 200 vazio) e escrita (500 cru) — nenhum dos dois é o `401`/`403` que deveria acontecer.

### Teste 7 — role Owner vs Staff

1. Convite de equipe como `Staff` criado por Owner A: `POST /api/team/invitations` → `201`, token de convite capturado.
2. `POST /api/team/invitations/{token}/accept` → `200`, conta de Staff criada.
3. Login do Staff → `200`, token de Staff capturado.
4. Com o token de Staff:

| Tentativa | Esperado | Resultado real |
|---|---|---|
| `PATCH /api/financeiro/contas-a-pagar/{id}/pagar` (Owner-only) | 403 | **403** |
| `POST /api/team/invitations` (Owner-only) | 403 | **403** |
| `PUT /api/tenants/branding` (Owner-only) | 403 | **403** |
| `GET /api/customers` (sem restrição de role) | 200, só dados do próprio tenant | **200**, devolveu só o cliente do Tenant A (o único tenant do Staff) |

`RequireRole("Owner")` funciona corretamente em todos os endpoints testados.

### Teste 8 — achado P0-1, explorado de ponta a ponta

```
# Plano público (endpoint anônimo por design, preço não é dado sensível):
GET /api/billing/plans
-> [{"id":"22222222-2222-2222-2222-222222222222","name":"Grátis",...}, {"id":"11111111-...","name":"Padrão","priceAmount":99.00,...}]

# Nenhum header de autenticação, nenhuma prova de posse do Tenant B:
POST /api/billing/subscription/onboard-select-plan
Content-Type: application/json
{"tenantId":"5bdd53a0-37f8-4e11-a4d7-a6907adc829a","planId":"22222222-2222-2222-2222-222222222222"}

-> 200 {"requiresPayment":false,"checkoutLink":null}

# Confirmação, agora COM o token legítimo do Owner B, que nunca chamou onboard-select-plan:
GET /api/billing/subscription   (Authorization: Bearer <token B>)
-> 200 {"planName":"Grátis","status":"Active","trialEndsAtUtc":"2026-09-02T02:26:14Z",...}
```

A assinatura do Tenant B foi ativada por um chamador completamente anônimo. `tenantId` não é segredo: o também-anônimo `GET /api/tenants/by-slug/{slug}` devolve o `id` (GUID) do tenant a partir do slug, e o slug costuma ser escolhido pelo próprio dono do negócio (ex.: nome do salão) — nada impede alguém de descobrir/adivinhar o slug de um concorrente e mexer no plano dele durante a janela de trial. O único guard em `OnboardSelectPlanCommandHandler` é `subscription.Status is SubscriptionStatus.Active` — ou seja, a janela de exploração dura o trial inteiro, até o dono terminar o onboarding de verdade.

`GET /api/billing/subscription/onboard-status?tenantId=...` tem o mesmo problema de design (anônimo, tenantId no query string), mas hoje só devolve `{"isReady": bool}` — impacto de confidencialidade baixo por si só, o problema real está no endpoint de escrita acima.

---

## Suíte automatizada de isolamento

```
dotnet test backend/tests/Agendio.IntegrationTests -c Release --filter "FullyQualifiedName~Tenant|FullyQualifiedName~CrossTenant"

Aprovado! – Com falha: 0, Aprovado: 73, Ignorado: 0, Total: 73, Duração: 1m19s
```

Cobertura automatizada de isolamento **já existe e passa**. Os testes ao vivo acima não encontraram nenhum caso que a suíte não cubra — reforça que camadas 2 (Global Query Filter) e 3 (RLS) estão sólidas; o achado P0 é numa área (endpoint anônimo de billing) que por definição não passa pelas mesmas checagens de tenant e por isso não tem teste de isolamento cruzado cobrindo-a.

---

## Achados de revisão de código (detalhe)

### P1-2 — grep que confirma a ausência do "TenantMiddleware"

```
$ grep -rn "TenantMiddleware" backend/src
backend/src/shared/Agendio.Infrastructure/Multitenancy/HttpTenantContext.cs:18:
    /// TenantMiddleware (Agendio.Api) roda antes de qualquer endpoint autenticado e
```

Único resultado é o próprio comentário — a classe/middleware descrita não existe. `Program.cs` (lido por completo) não registra nenhum middleware de resolução de tenant por subdomínio/slug; a única validação de tenant que existe é: JWT → claim `tenant_id` → `HttpTenantContext` → Global Query Filter → RLS. Isso não é uma falha de isolamento *hoje* (confirmado nos testes ao vivo), mas é uma divergência entre o modelo de segurança documentado no `CLAUDE.md` (3 camadas independentes) e o que está implementado (2 camadas).

### P2-1 — N+1 em `GetCustomerRecoveryCandidatesQueryHandler`

```csharp
// backend/src/modules/Scheduling/.../GetCustomerRecoveryCandidatesQueryHandler.cs:76-88
var results = new List<CustomerRecoveryCandidate>();
foreach (var (customerId, stats) in overdueByCustomerId.OrderByDescending(kv => kv.Value.DaysOverdue))
{
    var customer = await customerLookup.FindByIdAsync(customerId, cancellationToken); // 1 query por candidato
    ...
}
```

`ICustomerLookupService` (`backend/src/modules/Customers/Agendio.Modules.Customers.Contracts/ICustomerLookupService.cs`) só expõe `FindByIdAsync` — não há `FindByIdsAsync`/batch, então não dá para corrigir sem adicionar um método ao contrato do módulo Customers.

---

## O que não foi testado (bloqueios/fora do escopo desta rodada)

- **MFA cross-tenant**: não testado (exigiria configurar TOTP real por tenant — fora do escopo do teste de isolamento pedido).
- **Webhooks Asaas** (billing e sinal de agendamento): a validação de segredo por `FixedTimeEquals` foi só lida no código, não testada ao vivo (exigiria o segredo real configurado e simular payload da Asaas).
- **Rate limiting como controle de segurança**: observado incidentalmente (a suíte de teste bateu no limite de `auth` uma vez, confirmando que o limiter está ativo), mas não foi objeto de teste dedicado de bypass.
- **Upload de arquivos** (logo/banner/foto): não testado quanto a validação de tipo/tamanho real (só a rota de autorização foi revisada).
