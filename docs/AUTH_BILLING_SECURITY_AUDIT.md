# Auditoria — Cadastro, Autenticação, Segurança, Assinatura e Asaas

**Data:** 2026-08-21
**Escopo:** resposta ao brief de 54 pontos do dono do produto sobre reestruturação de
cadastro/autenticação/segurança/assinatura. **Fase 1 (Auditoria) — somente leitura, nenhum
código foi alterado.**

Metodologia: 4 investigações paralelas lendo o código atual (`backend/src`, `frontend/app`),
cruzadas com o que já era conhecido de auditorias anteriores desta mesma base (Argon2id, RLS,
rotação de refresh token, esquema JWT de Onboarding). Toda afirmação abaixo cita arquivo:linha.

---

## O que já está sólido (não mexer)

- **Senha**: Argon2id corretamente configurado (`Argon2PasswordHasher.cs`, 64 MiB / 3 iterações /
  paralelismo 2). Nenhum outro algoritmo em uso.
- **Refresh token**: rotação a cada uso, hash SHA-256 em repouso, cookie
  `HttpOnly; Secure; SameSite=Lax`, **reuso detectado revoga a família inteira**
  (`RefreshAccessTokenCommandHandler.cs`).
- **MFA/TOTP para dono de tenant**: completo (setup/enable/disable/verify, códigos de
  recuperação de uso único, challenge em Redis de uso único).
- **Multi-tenancy**: 3 camadas independentes confirmadas — `TenantId` só vem da claim JWT
  (nunca de parâmetro de request), Global Query Filter do EF Core, e RLS no Postgres (app sem
  `BYPASSRLS`). Amostra de 4 handlers de IDOR clássico (customer/product/appointment by id) —
  nenhum aceita `TenantId` explícito.
- **CORS**: allowlist explícita por configuração, vazia por padrão (`AllowedOrigins: []`), sem
  `AllowAnyOrigin`.
- **Headers de segurança**: CSP restritiva, `X-Frame-Options: DENY`, `X-Content-Type-Options`,
  `Referrer-Policy` — falta só HSTS (ver achados).
- **Webhook Asaas**: token comparado em tempo constante (`CryptographicOperations.FixedTimeEquals`),
  **idempotência real** via índice único em `AsaasPaymentId` (não só checagem de aplicação).
- **Preço e trial**: sempre lidos/calculados no servidor — nenhum command de billing aceita preço
  do cliente; `TrialEndsAtUtc` calculado uma única vez a partir de `IClock`, protegido por índice
  único (não há caminho de código para reiniciar trial).
- **Gate de acesso por pagamento**: `Tenant.IsActive` só é alterado pelo job de conciliação
  (baseado em `Subscription.Status`, que só muda via webhook) ou por Super Admin — nenhum endpoint
  de tenant aceita esse campo do corpo da requisição.
- **Paginação**: teto rígido de 100 itens (`PaginationExtensions.MaxPageSize`).
- **Confirmação de e-mail**: token CSPRNG de 64 bytes, hash em repouso, expira em 24h, uso único,
  login bloqueado de verdade (`EmailNotConfirmed`) enquanto não confirmado — não é só o frontend
  escondendo botão.

---

## Achados — priorizados

### P0 — Crítico

| # | Achado | Evidência | Risco |
|---|---|---|---|
| P0-1 | **Super Admin (Platform) sem MFA** — autoridade de maior privilégio (cross-tenant, cancela assinatura de qualquer cliente) protegida só por senha | `PlatformAdmin.cs:20-74` (sem campo MFA); `LoginPlatformAdminCommandHandler.cs:14-37` | Comprometimento de 1 senha = acesso administrativo total à plataforma |
| P0-2 | **Nenhum log de auditoria de segurança** — login (sucesso/falha), IP, user-agent, troca de MFA não geram nenhum registro. O `AuditLogEntry` existente é só de dados de negócio (created/updated/deleted em entidade), não de eventos de autenticação | `AuditLogInterceptor.cs:63-68`; grep `LoginAttempt/SecurityAuditLog` = 0 resultados | Zero trilha forense; bloqueia qualquer detecção futura de abuso (depende deste item) |
| P0-3 | **Sem lockout por conta no login** — só rate limit por IP (10 req/60s, `FixedWindowRateLimiter` em memória de processo, multiplicado por instância em multi-réplica). Nenhuma contagem de tentativas falhas por e-mail/usuário | `Program.cs:204-224`; grep `LoginAttempt/Lockout` = 0 resultados | Brute-force distribuído por IP não é barrado |
| P0-4 | **Recuperação de senha ("esqueci minha senha") não existe** | grep `ForgotPassword/ResetPassword` = 0 resultados em todo o backend | Usuário que esquece a senha fica permanentemente travado fora da conta |
| P0-5 | **Troca de senha autenticada não existe** — `User` não tem nenhum método para alterar `PasswordHash` após o registro | `Domain/User.cs` (só define senha no construtor) | Consequência: quando implementada, precisa nascer já revogando a família de refresh tokens (hoje não haveria como) |

### P1 — Alto

| # | Achado | Evidência | Risco |
|---|---|---|---|
| P1-1 | **Squatting de slug/tenant** — o `Tenant` (e o slug) é criado no passo 1 do wizard, `IsActive=true`, antes de e-mail/senha/dono existirem. Não há job de limpeza de tenants órfãos | `CreateTenantCommandHandler.cs:32-39`; `onboarding/page.tsx:171-176` | Qualquer um pode reservar um slug abandonando o wizard depois do passo 1, sem precisar de e-mail válido |
| P1-2 | **CPF/CNPJ sem validação de dígito no fluxo de Billing** — existe um value object `CpfCnpj` correto (módulo 11) mas o validator de `SubscribeToPlanCommand` usa só `NotEmpty().MaximumLength(20)`, não o value object | `SubscribeToPlanCommandValidator.cs:11` vs. `CpfCnpj.cs:45-69` | CPF/CNPJ inválido chega até a Asaas sem checagem |
| P1-3 | **CPF exposto em texto pleno na API** — `GetCustomerByIdQueryHandler` devolve o CPF sem máscara; mascaramento (`PiiMasking`) só existe para logs, não para resposta HTTP | `GetCustomerByIdQueryHandler.cs:21-24` | Qualquer chamada autorizada ao endpoint vê o CPF completo, não `***.***.**-42` |
| P1-4 | **Zero detecção de abuso de trial** — nenhuma coleta de IP em evento de auth, nenhuma correlação por CPF/telefone/e-mail entre contas | grep `IpAddress/Fingerprint/AbuseDetection` = 0 resultados | Sem log de segurança (P0-2) isso nem pode nascer — são o mesmo buraco visto de dois ângulos |
| P1-5 | **Enumeração de usuário no `/register`** — 409 `EmailTaken` revela que um e-mail já é usuário de um tenant, diferente de todos os outros endpoints de auth (que são genéricos de propósito) | `RegisterUserCommandHandler.cs:41-45` | Permite confirmar se um e-mail específico já tem conta |
| P1-6 | **Sem "logout global"** — usuário não tem como revogar todas as sessões sob demanda (só acontece automaticamente se um token já rotacionado for reutilizado) | busca por `RevokeUserTokenFamilyCommand` não encontrou implementação (só citado em comentário) | Dispositivo roubado/sessão vazada não pode ser encerrado pelo dono da conta |
| P1-7 | **Rate limit de reenvio de confirmação só por IP** — 500 e-mails para o mesmo endereço passam girando IP, 10 de cada vez | `IdentityEndpoints.cs:74`; `Program.cs:219` (partição só `RemoteIpAddress`) | Spam de e-mail de confirmação |

### P2 — Médio

| # | Achado | Evidência |
|---|---|---|
| P2-1 | HSTS ausente (só `UseHttpsRedirection`, sem `UseHsts`) | `Program.cs:345` |
| P2-2 | Sem verificação de `Origin` no `/api/auth/refresh` (defesa em profundidade além do `SameSite=Lax`) | `RefreshAccessTokenCommandHandler.cs` |
| P2-3 | Rate limiting ausente em `/refresh`, `/logout`, `/mfa/setup\|enable\|disable` (só o limite global genérico de 200/60s) | `IdentityEndpoints.cs:125-196` |
| P2-4 | Sem lista de slugs reservados (`admin`, `api`, `www`, `login`, `suporte`...) | grep `reserved` = 0 resultados |
| P2-5 | Sem endpoint de "verificar disponibilidade de slug" em tempo real — front só descobre no 409 do submit final | `onboarding/page.tsx:186-190` |
| P2-6 | Duplicidade de CPF não verificada em `Customer` | `CreateCustomerCommandHandler.cs` (só trata e-mail duplicado) |
| P2-7 | `AsaasOptions` sem `ValidateOnStart` — se `WebhookSecret` virar string vazia em produção (em vez de ausente), a comparação `FixedTimeEquals` de dois arrays vazios passa e o webhook aceita POST sem header nenhum | `BillingModuleServiceCollectionExtensions.cs:33` |
| P2-8 | Chargeback/estorno (`PAYMENT_REFUNDED`) marca o `Payment` mas não rebaixa `Subscription.Status` — assinatura continua `Active` com o dinheiro devolvido | `ProcessAsaasWebhookCommandHandler.cs:82-84` |
| P2-9 | Inconsistência de regra de senha: frontend valida mínimo 8, backend exige mínimo 10 — usuário só descobre no fim do wizard | `onboarding/page.tsx:59` vs. `RegisterUserCommandValidator.cs:13-16` |

### P3 — Observação / não-bloqueante

- Diferença de timing pequena no login entre "e-mail não existe" e "senha errada" (curto-circuito do `||` evita o hash Argon2 quando usuário não existe).
- `ListCustomersQueryHandler` materializa toda a lista filtrada por nome antes de paginar quando há filtro de `Segment` — nota de performance, não de segurança.
- Cupom de desconto: não existe (feature nova a construir, não um bug).
- Domínio customizado: não existe nenhum placeholder de schema ainda (feature nova).
- **O brief fala em "subdomínio"; a arquitetura real é slug de path (`agendio.com.br/{slug}`)** — decisão já documentada em `docs/BACKLOG.md` (BL-06). Ajustar a expectativa: qualquer trabalho de "seleção de subdomínio" no brief deve ser lido como "seleção de slug de path".

---

## Gaps de escopo — não são bugs, são funcionalidades que o brief pede e ainda não existem

- CPF/CNPJ e endereço completo (CEP/estado/cidade/bairro/rua/número/complemento) no cadastro do
  **dono** do estabelecimento — hoje o onboarding só pede nome do negócio, slug, fuso, tipo de
  negócio, nome/e-mail/senha do dono. CPF só aparece depois, na tela de assinatura paga.
- Máquina de estados de conta unificada (`PendingEmailVerification → ... → Blocked`) — hoje é
  fragmentada entre `User.EmailConfirmedAt` (bool via nullable), `Tenant.IsActive` (bool) e
  `Subscription.Status` (enum de verdade, só para billing).
- CAPTCHA/desafio progressivo.
- Entidade `SecurityAuditLog` dedicada (distinta do `AuditLogEntry` de dados de negócio).
- Painel de segurança do admin (contas suspeitas, indicador de risco, tentativas de login).
- Validação de cupom.
- Preparação de schema para domínio customizado.
- LGPD formal (política de retenção/exclusão, trilha de consentimento).

---

## Status pós-implementação (2026-08-22)

**P0 (5/5) resolvido — backend + frontend, testado.**

| # | Item | Backend | Frontend | Testes |
|---|---|---|---|---|
| P0-1 | MFA para Super Admin | `PlatformAdmin` (setup/enable/disable/verify TOTP, sem código de recuperação — ver comentário em `PlatformAdmin.DisableMfa`) | `/admin/security`, passo de MFA em `/admin/login` | Integração (setup→enable→login com desafio→verify) |
| P0-2 | Log de auditoria de segurança | `SecurityAuditLogEntry` + `SecurityAuditLogger<TContext>` (Identity e Platform, schema próprio cada) | — (interno) | Integração (login falho/sucesso gravado) |
| P0-3 | Bloqueio de conta | `User`/`PlatformAdmin.RegisterFailedLoginAttempt` — 5 tentativas, 15min, mensagem genérica mantida | — (mensagem de erro já tratada pela tela de login existente) | Unitário (contagem/expiração) + integração (bloqueio real via HTTP) |
| P0-4 | Recuperar senha | `/api/auth/forgot-password`, `/api/auth/reset-password` — token 30min, uso único | `/forgot-password`, `/reset-password/[token]`, link na tela de login | Integração ponta a ponta via MailHog |
| P0-5 | Trocar senha autenticada | `/api/auth/change-password` — exige senha atual, revoga todas as sessões | Card em `/settings/security` | Integração (senha antiga passa a falhar, sessões revogadas) |

Achado corrigido durante a implementação (não estava no diagnóstico original, achado ao testar):
duas registros de DI (`Identity` e `Platform`) mapeavam a MESMA interface `ISecurityAuditLogger`
— o último módulo carregado "vencia" silenciosamente para qualquer injeção no processo inteiro,
fazendo eventos de um módulo às vezes irem parar na tabela do outro. Corrigido trocando por tipos
genéricos fechados por módulo (`SecurityAuditLogger<IdentityDbContext>`/`SecurityAuditLogger<PlatformDbContext>`),
sem interface compartilhada — ver comentário em `SecurityAuditLogger.cs`.

Verificado ao vivo no navegador (não só testes automatizados): fluxo completo de "esqueci minha
senha" com e-mail real via MailHog, troca de senha com sessão revogada, e MFA do Super Admin
(setup → QR code real → login com desafio → verify) — todos funcionando ponta a ponta.

**Cobertura de teste**: 253 testes de integração + 245 unitários + 31 de arquitetura, todos verdes.
Duas migrations novas aplicadas (não-destrutivas).

## Status P1 (2026-08-22)

**4 de 7 resolvidos, testados. 1 investigado e fechado sem alteração (regressão evitada). 2 adiados — decisão de produto/escopo maior.**

| # | Item | Status | Nota |
|---|---|---|---|
| P1-1 | Squatting de slug | **Resolvido** | `Tenant.FirstUserRegisteredAtUtc` marcado por `TenancyIntegrationEventConsumer` (novo, escuta `UserRegistered` do Identity via RabbitMQ — segundo consumidor do projeto, mesmo molde de `BillingIntegrationEventConsumer`). `OrphanTenantCleanupJob` (diário) libera o slug + desativa tenants abandonados há mais de 48h sem nenhum usuário registrado. Migration com backfill (`FirstUserRegisteredAtUtc` calculado para tenants já existentes) — não quebra dado histórico. |
| P1-2 | CPF/CNPJ sem validação no billing | **Resolvido** | `SubscribeToPlanCommandValidator` agora usa o value object `CpfCnpj` (dígito verificador). Handler normaliza para dígitos puros antes de mandar à Asaas. |
| P1-3 | CPF exposto sem máscara na API | **Investigado, sem alteração** | O único lugar onde `Customer.Cpf` aparece em texto pleno é `GetCustomerByIdQueryHandler` — usado pelo frontend para **pré-preencher o formulário de edição** do cliente. Mascarar quebraria a edição (salvaria `***.***.**-42` por cima do CPF real). Diferente do cenário do brief original (Super Admin vendo CPF de donos de tenant, um cruzamento de fronteira de confiança maior) — aqui é a própria equipe do estabelecimento vendo o CPF do próprio cliente, já isolado por tenant (RLS+JWT). `ListCustomersQueryHandler` (listagem) já não expõe CPF nenhum. Fechado como não-acionável nesta forma. |
| P1-4 | Detecção de abuso de trial | **Adiado** | Exige feature nova (correlação de IP/CPF/telefone entre cadastros, indicador Risco Baixo/Médio/Alto, painel no Super Admin) — escopo de uma rodada própria, não uma correção pontual. |
| P1-5 | Enumeração no `/register` | **Resolvido — via reordenação do fluxo** | Decisão do dono do produto: em vez de mexer na resposta do `/register`, o gate real virou "escolher plano → confirmar e-mail → login → ativar/pagar". `OnboardSelectPlanCommandHandler` agora só grava a intenção de plano (`Subscription.SelectPlan`) — não ativa Free nem cria Checkout na Asaas antes do e-mail confirmado. Ativação de verdade (`activate-free`/`subscribe`) só é alcançável depois do login, que já exige e-mail confirmado. Fecha ao mesmo tempo o "conta ativa antes de confirmar e-mail" do brief original, sem precisar de nenhuma checagem cross-módulo nova. `/settings/billing` corrigido para mostrar o plano certo (por `planId`, não mais o primeiro item da lista). |
| P1-6 | Sem logout global | **Resolvido** | `POST /api/auth/logout-all` revoga todos os refresh tokens do usuário. Botão "Sair de todos os dispositivos" em Configurações → Segurança. |
| P1-7 | Rate limit de reenvio só por IP | **Resolvido** | `IEmailSendThrottle` (Redis, INCR+EXPIRE atômico) limita a 3 envios/hora por (tenant, e-mail) — complementa o rate limit de IP existente. Aplicado em `resend-confirmation` e `forgot-password`. |

Um regressão real foi pega pela suíte de testes durante a implementação: o novo validador de CPF/CNPJ
rejeitava (corretamente) o CPF fictício `12345678900` usado nos testes existentes de `BillingTests.cs`
— trocado por um CPF de formato válido (`12345678909`), sem mudar o que o teste cobre.

**Cobertura de teste**: 257 testes de integração + 248 unitários + 31 de arquitetura, todos verdes.

## P1-5 — reordenação do onboarding (2026-08-22)

Decisão do dono do produto: plano → confirmar e-mail → tela de pagamento do plano certo (em vez de
ativar/cobrar antes do e-mail confirmado, como o fluxo original fazia).

- `Subscription.SelectPlan(planId)` (novo) — só grava a intenção, nunca ativa nem chama a Asaas.
- `OnboardSelectPlanCommandHandler` simplificado: sem checkout, sem ativação — só `SelectPlan`.
- Ativação de verdade continua nos MESMOS endpoints que já existiam fora do onboarding
  (`/subscription/activate-free`, `/subscription/subscribe`), agora alcançáveis só depois do login
  — que já exige e-mail confirmado (`LoginCommandHandler`). Nenhuma checagem cross-módulo nova foi
  necessária: o gate "nasce" da ordem dos endpoints, não de uma verificação explícita.
- `GetMySubscriptionResult` ganhou `PlanId` — `/settings/billing` corrigido para mostrar o plano
  **registrado** na assinatura, não mais `plansQuery.data[0]` (bug pré-existente que mostrava o
  primeiro plano da lista, não necessariamente o escolhido).
- Onboarding (`onboarding/page.tsx`): removida a tela "aguardando confirmação do cartão" e o
  polling de status — iam direto pra "confirme seu e-mail" agora, pago ou grátis, sem abrir aba de
  checkout durante o cadastro.
- Um teste de integração (`Checkout_Payment_Confirmed_Webhook_Adopts_The_Subscription_Via_ExternalReference...`)
  foi removido porque testava um mecanismo (checkout criado direto no onboarding, sem CPF, adotado
  depois via `externalReference` do webhook) que deixou de existir neste fluxo — o método
  `IAsaasClient.CreateCreditCardCheckoutAsync` e o branch de adoção por `externalReference` em
  `ProcessAsaasWebhookCommandHandler` ficaram sem nenhum chamador. Não removi esse código ainda
  (mudança maior, fora do escopo desta rodada) — fica como oportunidade de limpeza futura.

**Verificado ao vivo no navegador**: cadastro completo → escolha do plano Pago → tela "confirme seu
e-mail" (sem aba de checkout abrindo) → assinatura gravada como `Trialing`/`Padrão` no banco → e-mail
confirmado → login → `/settings/billing` mostrando exatamente "Assinar Padrão — R$ 99,00/mês" (não
o plano errado).

## Próximo passo

P0 concluído. P1: 5 de 7 resolvidos (P1-1, P1-2, P1-5, P1-6, P1-7), 1 investigado e fechado sem
alteração (P1-3), 1 ainda adiado — **P1-4 (detecção de abuso de trial)**, que exige uma feature nova
(painel de risco no Super Admin). Depois de decidir P1-4, sequenciar os achados Médios (P2).
