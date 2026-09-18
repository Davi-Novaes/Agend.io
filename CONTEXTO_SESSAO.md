# Contexto do Agendio — para iniciar um chat novo

> Cole este arquivo (ou peça pra Claude ler `CONTEXTO_SESSAO.md` na raiz do repo) no início de uma conversa nova para retomar o trabalho sem precisar re-explorar o projeto do zero. Gerado em 2026-08-16.

## O que é o sistema

**Agendio** — SaaS multi-tenant de gestão para negócios baseados em agendamento (barbearia, salão, clínica odontológica/médica/estética, psicólogo, fisioterapeuta, nutricionista, personal trainer, academia, pet shop, lava-rápido, oficina, advocacia, contabilidade, estúdio de foto, etc.). Cada segmento usa o mesmo produto com terminologia adaptada (ex.: "Cliente" vira "Clientes", "Profissional" vira "Barbeiros" numa barbearia).

`Agendio` é nome de trabalho — trocar é find/replace em `backend/` e `frontend/`.

## Stack e arquitetura

- **Backend**: .NET 10, modular monolith. `backend/src/host/Agendio.Api` (composition root, Minimal APIs) + `backend/src/shared/{Agendio.SharedKernel, Agendio.Infrastructure}` + 12 módulos em `backend/src/modules/<Modulo>/` (cada um com `Domain/Application/Infrastructure/Endpoints/`), 9 deles com um projeto `.Contracts` irmão pra expor superfície pública cross-módulo.
- **Frontend**: Next.js 16 (App Router) + React 19, TypeScript, Tailwind v4, shadcn/Radix, TanStack Query, React Hook Form + Zod, Recharts.
- **Dados**: PostgreSQL (RLS real habilitada, não só global query filter do EF), Redis (challenges de MFA, cache), RabbitMQ (integration events entre módulos), Hangfire (jobs — notificações, e-mails, cobrança).
- **CQRS com dispatcher próprio** (`ICommandHandler`/`IQueryHandler`, sem MediatR — licença comercial), `Result<T>` em vez de exceção pra regra de negócio, FluentValidation em pipeline behavior, Mapperly (não AutoMapper), ids fortemente tipados, NodaTime + `IClock` (nunca `DateTime.Now/UtcNow` direto), Argon2id pra senha.
- Convenções completas e obrigatórias estão em `CLAUDE.md` na raiz — carregado automaticamente pelo Claude Code, mas vale ler se for onboarding humano.

### Multi-tenancy — três camadas independentes (defesa em profundidade)
1. Resolução do tenant por subdomínio/slug + claim `tenant_id` no JWT (divergência = 403).
2. Global query filter do EF Core.
3. Row Level Security no PostgreSQL — a aplicação conecta com role **sem `BYPASSRLS`**.

Toda feature que toca dado de tenant tem teste de isolamento cruzado. Nunca remover uma camada "porque a outra já cobre".

### Módulos backend (12)
`Identity` (auth, MFA, refresh token rotation), `Tenancy` (Tenant + Unit), `Customers` (CRM, fidelidade), `Catalog` (serviços), `Resources` (profissionais/salas, folgas), `Scheduling` (motor de agendamento — sem overbooking via exclusion constraint no Postgres, avaliações, lista de espera, no-show, depósito, notificações), `Billing` (assinatura da própria plataforma via Asaas), `Financeiro` (contas a pagar/receber, comissões, fluxo de caixa), `Estoque` (produtos revendidos), `Marketing` (campanhas multi-canal), `Assistant` (chat de IA multi-provider), `Platform` (Super Admin, autoridade separada do tenant).

### Segurança — inegociável
JWT de acesso 15min em memória no frontend (nunca localStorage/cookie legível), refresh token rotativo hasheado em cookie `HttpOnly;Secure;SameSite=Lax` (reuso revoga a família inteira), Super Admin como `scope: platform` separado (nunca um papel dentro de tenant), CPF/dados de saúde criptografados em coluna (AES-256-GCM), Serilog redige dado sensível de log, rate limiting por tenant, MFA/TOTP com códigos de recuperação.

### Decisões arquiteturais registradas (`docs/adr/`)
0001 multi-tenancy com RLS · 0002 exceção de RLS pra achar refresh token pelo hash · 0003 um DbContext por módulo · 0004 substituição de MediatR/AutoMapper/FluentAssertions (licença) · 0005 rotação de refresh token com detecção de reuso · 0006 ordem do rate limiter middleware · 0007 chave de criptografia de coluna em appsettings (sem KMS) · 0008 estado de MFA em Redis + códigos de recuperação em tabela dedicada.

## Estado atual (2026-08-16)

**Tudo commitado e no `origin/main`** (push feito nesta sessão, commit `0e67fa1`). 42 commits, um por fase/etapa. Não há branch de feature aberto nem plano pendente em `~/.claude/plans/`.

Suíte de testes: ~480 testes (unit + arquitetura + integração via Testcontainers com Postgres/Redis/RabbitMQ reais) passa 2x seguidas antes de qualquer commit — convenção seguida em toda a história do projeto. `dotnet build` com 0 warnings de compilador (só um aviso de vulnerabilidade transitiva do pacote `SSH.NET`, dependência do Testcontainers, não relacionado a código do projeto). Frontend: `tsc --noEmit`/`eslint`/`build` limpos.

### Funcionalidade entregue (cobertura muito ampla — resumo por área)

- **Fundação**: onboarding com templates por segmento, MFA/TOTP, rate limiting, criptografia de coluna, auditoria.
- **Operação**: perfil/branding/customização da página pública, horário de funcionamento + folgas + datas fechadas + buffer, motor de agendamento sem overbooking, **multi-unidade** (opcional, zero fricção pra tenant de unidade única), portal público com QR/compartilhamento, cancelamento/reagendamento com motivo e log, lista de espera, política de no-show, depósito obrigatório via Asaas.
- **CRM**: perfil do cliente com histórico, auto-segmentação, recuperação de clientes inativos, programa de fidelidade, avaliações pós-atendimento, notificações por **e-mail e WhatsApp** (6 templates, toggles configuráveis) com histórico.
- **Financeiro/Estoque/Marketing**: contas a pagar/receber, comissão por profissional, fluxo de caixa, categoria/custo de produto com alerta de estoque baixo, campanhas multi-canal por segmento.
- **Relatórios/Dashboard**: painel cruzando Financeiro+Agenda+Estoque, insights automáticos, comparação com período anterior.
- **IA**: módulo `Assistant` — chat multi-provider (Anthropic/OpenAI/DeepSeek), escopo de dados restrito a agregados de Relatórios.
- **Billing da própria plataforma** (mais recente — Fases 23-24): confirmação de e-mail obrigatória antes do primeiro login; onboarding exige escolher plano — Free ativa na hora sem cartão, plano pago abre **Asaas Checkout hospedado** (cartão nunca passa pelo backend, correlação por `externalReference` no webhook).
- **Painel administrativo** (`/admin`, Super Admin): dashboard com métricas do SaaS (MRR, tenants ativos/novos, distribuição de assinaturas), cancelamento de assinatura de tenant.
- **Redesign visual completo**: design system (paleta oklch, Plus Jakarta Sans, dark mode via `next-themes`), shell Sidebar/Header, dashboard com gráficos reais (Recharts, seguindo a skill `dataviz`), reskin de todas as páginas autenticadas, landing page, `AlertDialog` de confirmação em ações financeiras irreversíveis.

## Pendências conhecidas (verificadas por leitura de código, não por memória acumulada)

1. **CI provavelmente quebrado para o teste e2e autenticado.** `frontend/e2e/financeiro.spec.ts` chama a API real pra criar um tenant antes do axe-core rodar, mas o job `frontend` do `.github/workflows/ci.yml` só sobe `npm run start` (sem backend/Postgres/Redis/RabbitMQ). `npm run test:e2e` roda tudo dentro de `e2e/` sem filtro, então esse teste deveria falhar em todo push desde que foi criado. **Não confirmado via GitHub Actions UI** (`gh` CLI indisponível no ambiente da sessão anterior) — checar o histórico de Actions antes de decidir a correção. Opções: subir a infra completa no job `frontend`, mover o teste pro job `backend`, ou excluir do CI via `testIgnore` e manter só localmente.
2. **Sem nome de exibição do usuário em lugar nenhum do sistema.** `AuthTokenIssuer.BuildClaims` (`backend/src/modules/Identity/Agendio.Modules.Identity/Application/AuthTokenIssuer.cs`) só carrega `NameIdentifier`/`Email`/`Role`/`TenantId`/`TenantSlug` no JWT — sem `FullName`. O Header do painel mostra só iniciais do e-mail.
3. **Cortes de escopo deliberados** (não são bugs): ranking de clientes no Relatórios (exigiria `CustomerId` no evento de conclusão de agendamento), segmentação avançada/aniversário automático no Marketing, múltiplos planos pagos além de Free/Padrão, cobrança imediata no ato da assinatura (cobra só no fim do trial, por design).

Nenhuma pendência de segurança/infraestrutura em aberto até onde foi auditado — houve uma auditoria completa dedicada (RLS, autorização, comparação constant-time em webhook etc.) antes do roadmap de fases começar, e todos os achados foram corrigidos.

## Comandos úteis

```bash
docker compose -f infra/docker-compose.yml up -d   # Postgres:5432, Redis:6379, RabbitMQ:5672 (UI:15672), Seq:8081, MailHog SMTP:1025 (UI:8025)
dotnet build backend/Agendio.slnx
dotnet test backend/Agendio.slnx                    # rodar 2x antes de commitar, sempre
dotnet run --project backend/src/host/Agendio.Api    # API em localhost:5071
cd frontend && npm run dev                           # localhost:3000
```

Ao gerar uma migration nova (`dotnet ef migrations add ...`), lembrar de rodar `dotnet ef database update --project <modulo> --startup-project backend/src/host/Agendio.Api` antes de testar manualmente no navegador — senão toda rota do módulo novo dá 500 silenciosamente (gotcha já encontrado múltiplas vezes).

## Como retomar o trabalho

1. `git log --oneline` é a fonte confiável de "o que foi feito, em que ordem" — mensagens seguem o padrão `Fase N: <resumo>` ou `Etapa N: <resumo>`. Pra detalhes técnicos de uma fase específica, ler o commit/diff correspondente, não confiar em resumo acumulado.
2. Fluxo padrão de toda feature nova neste projeto: pergunta direta de escopo ao usuário → plan mode (Explore + Plan agent) → implementação → testes → verificação manual no navegador → commit (push só com aprovação explícita, salvo instrução em contrário).
3. Não há próxima fase pré-definida no momento — o roadmap conhecido (24 fases de negócio + 9 etapas de redesign visual + billing da plataforma) está fechado. Início natural de uma sessão nova: perguntar ao usuário a próxima prioridade, ou investigar a pendência 1 (CI) como possível quick-fix.
