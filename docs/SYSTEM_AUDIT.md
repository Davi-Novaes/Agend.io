# Auditoria de Sistema — Fase 1 (Reconhecimento) — Agendio

> Fase somente-leitura. Nenhum código foi alterado pra produzir este documento. Base: exploração direta do repositório nesta sessão + histórico de commits (`git log`) + testes já existentes.

## O que é

SaaS multi-tenant de gestão para negócios baseados em agendamento (barbearia, salão, clínica, psicólogo, pet shop, lava-rápido, advocacia, etc.), com três públicos: Super Admin da plataforma, dono/equipe do estabelecimento, e cliente final (via página pública).

## Stack

- **Backend**: .NET 10, modular monolith. Composition root em `backend/src/host/Agendio.Api`. CQRS com dispatcher próprio (sem MediatR — licença). Mapperly (sem AutoMapper). Ids fortemente tipados. NodaTime + `IClock` (nunca `DateTime.Now/UtcNow` direto). Argon2id pra senha.
- **Frontend**: Next.js 16 (App Router) + React 19, TypeScript, Tailwind v4, shadcn/Radix, TanStack Query, React Hook Form + Zod, Recharts.
- **Dados**: PostgreSQL com Row Level Security REAL habilitada (role de aplicação sem `BYPASSRLS`), Redis (MFA/cache), RabbitMQ (integration events entre módulos), Hangfire (jobs assíncronos: e-mail, cobrança, notificações).

## Módulos (12)

`Identity, Tenancy, Customers, Catalog, Resources, Scheduling, Billing, Financeiro, Estoque, Marketing, Assistant, Platform` — 9 deles com projeto `.Contracts` irmão expondo superfície pública pra outros módulos consumirem (comunicação cross-módulo só via `.Contracts` síncrono ou integration event assíncrono — nunca leitura direta de tabela de outro módulo; regra verificada por `Agendio.ArchitectureTests`).

## Multi-tenancy — três camadas (CLAUDE.md exige todas simultaneamente)

1. Resolução por subdomínio/slug + claim `tenant_id` no JWT (divergência = 403).
2. Global query filter do EF Core por `TenantId`.
3. Row Level Security no Postgres (role de app sem bypass).

Exceções documentadas em ADR: busca de refresh token por hash (antes do tenant ser conhecido) usa `IgnoreQueryFilters` — motivo: hash de 512 bits já é chave de busca segura, RLS da tabela `refresh_tokens` tem exceção pra essa consulta específica.

## Autenticação (estado após esta sessão)

- Access token JWT 15min em memória no frontend (nunca localStorage). Refresh token 30 dias rotativo, hasheado, cookie `HttpOnly;Secure;SameSite=Lax`, path restrito a `/api/auth`. Reuso de refresh revoga a família inteira (testado, `RefreshTokenFlowTests`).
- **Corrigido nesta sessão**: sessão não sobrevivia a reload de página (faltava tentar `/api/auth/refresh` no mount) — agora corrigido, com renovação proativa antes do access token expirar.
- **Corrigido nesta sessão**: não existia endpoint de logout real (token continuava válido no servidor após "Sair") — `POST /api/auth/logout` criado, revoga só a sessão atual.
- MFA/TOTP com códigos de recuperação, estado em Redis.
- Confirmação de e-mail obrigatória antes do primeiro login (Fase 23).

## Billing da própria plataforma

Onboarding exige escolher plano — Free ativa na hora sem cartão; plano pago abre Asaas Checkout hospedado (cartão nunca passa pelo backend, correlação por `externalReference` no webhook). Painel Super Admin com métricas (MRR, tenants ativos/novos) e cancelamento de assinatura.

## Testes existentes

~480 testes (unit xUnit v3 + Shouldly, integração via Testcontainers com Postgres/Redis/RabbitMQ reais, arquitetura). Convenção do projeto: rodar 2x antes de commit (nesta auditoria, sobe pra 5x conforme instrução do usuário). Nenhum teste de UI automatizado (Playwright) confirmado rodando no CI — ver bloqueio abaixo.

## CI/CD — bloqueio conhecido, não resolvido

Todas as runs do GitHub Actions falham em ~3 segundos sem executar nenhum step (`conclusion: failure`, array de `steps` vazio — padrão de `startup_failure`), desde a primeira run do repositório. Diagnosticado numa sessão anterior via API REST do GitHub (sem `gh` CLI disponível): repo é público (então não é limite de billing de minutos). Causa mais provável: Actions desabilitado ou restrito nas configurações do repo/conta, ou verificação de conta pendente — ambas exigem acesso autenticado do dono pra confirmar. **Nenhum commit dos últimos ~24 foi validado por CI.** Isso é relevante pra esta auditoria: os testes que "passam localmente" (confirmados nesta sessão, 225/225 de integração 2x seguidas antes de cada commit) são a única rede de segurança real no momento.

## Estado do front-end (nesta sessão)

Redesign completo do Dashboard (`/painel`): novo layout com agenda do dia, alertas acionáveis, ranking de serviços, indicadores de clientes, gráfico de status em rosca, tema escuro mais próximo de referência visual fornecida pelo usuário. Demais páginas (`/agenda`, `/clientes`, `/financeiro`, `/estoque`, `/marketing`, páginas de configuração, página pública `/[slug]`) **não foram revisadas nesta sessão** — é justamente o que a Fase 2 (Frontend Specialist) está verificando agora.

## Pendências já conhecidas antes desta auditoria (não re-verificadas ainda)

1. Sem forma do cliente final se autocadastrar/logar pra ver histórico — hoje o agendamento público é feito sem conta, só com nome/telefone/e-mail informados na hora. Decisão de produto pendente com o usuário (adiada explicitamente numa sessão anterior).
2. Página pública do estabelecimento (`/[slug]`) descrita pelo usuário como "muito simples"/pouco convidativa — ainda não trabalhada.
3. Cortes de escopo deliberados documentados: ranking de clientes em Relatórios, segmentação avançada/aniversário automático em Marketing, múltiplos planos pagos além de Free/Padrão.

## O que esta Fase 1 NÃO cobre (verificado na Fase 2 em paralelo, por agente)

- Teste real de isolamento cross-tenant via API (não só leitura de código).
- Teste de fluxos de negativos (dados inválidos, IDs de outro tenant, valores negativos, strings enormes).
- Revisão de UX/consistência visual das páginas fora do Dashboard.
- Avaliação comercial (onboarding, proposta de valor, pontos de abandono) sob a ótica de dono de estabelecimento real.
