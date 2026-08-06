# 0001 — Multi-tenancy: banco compartilhado com Row Level Security

## Status
Aceito (Sprint 0).

## Contexto
O sistema precisa isolar dados de milhares de estabelecimentos (tenants) com
custo operacional baixo por tenant, permitindo eventualmente promover um
cliente grande (franquia/rede) para infraestrutura dedicada sem reescrever a
aplicação.

## Decisão
Banco único (por módulo, ver ADR 0003), coluna `tenant_id` em toda tabela que
pertence a um tenant, com **três camadas independentes** de isolamento:

1. **Resolução do tenant**: claim `tenant_id` no JWT (rotas autenticadas) ou
   slug explícito no corpo da requisição (registro/login, antes de existir
   token — ver `IHasExplicitTenant`/`ExplicitTenantBehavior`).
2. **EF Core Global Query Filter**: toda entidade `ITenantOwned` filtra por
   `TenantId` automaticamente, reavaliado a cada query via referência viva ao
   `ITenantContext` (nunca um valor congelado no construtor do DbContext).
3. **PostgreSQL Row Level Security**: a aplicação conecta com a role
   `agendio_app`, que **não é dona das tabelas e não tem BYPASSRLS**. Mesmo que
   o filtro do EF falhe (bug, `IgnoreQueryFilters()` usado por engano), o banco
   recusa devolver linha de outro tenant.

Migrations rodam com uma role separada (`agendio_owner`), já que o dono de uma
tabela ignora RLS por padrão no PostgreSQL — usar a mesma role para app e
migration anularia a terceira camada de defesa.

## Consequências
- Onboarding de tenant é uma linha na tabela `tenants`, não uma migration/deploy.
- Qualquer feature nova que toque dado de tenant precisa de teste de isolamento
  cruzado (ver `Agendio.IntegrationTests.TenantIsolationTests`) — não é opcional.
- Cliente enterprise que exigir banco dedicado migra trocando connection string,
  não reescrevendo código, já que a chave de tenant já é explícita em tudo.
- Exceção documentada: o fluxo de refresh token localiza o registro pelo hash
  do token antes de saber a qual tenant ele pertence, exigindo uma política RLS
  com uma cláusula extra para esse caso específico — ver ADR 0002.
