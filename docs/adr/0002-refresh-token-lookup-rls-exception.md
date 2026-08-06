# 0002 — Exceção de RLS para localizar refresh token pelo hash

## Status
Aceito (Sprint 0).

## Contexto
O fluxo de refresh token (`POST /api/auth/refresh`) recebe só o token em texto
plano num cookie `HttpOnly` — nenhum JWT válido acompanha a requisição (o
access token pode já estar expirado, é justamente por isso que o cliente está
pedindo um novo). Isso significa que, no instante em que o handler precisa
localizar o registro de `RefreshToken` pelo hash do token, **o tenant ainda não
é conhecido**: `ITenantContext.HasTenant` é `false`.

Com o Global Query Filter (`rt.TenantId == CurrentTenantId()`, que retorna
`TenantId.Empty` quando não há tenant) e a política de RLS
(`tenant_id = current_setting('app.tenant_id')::uuid`, com a conexão setada
para o Guid vazio), a busca devolveria **zero linhas sempre**, mesmo para um
token válido — quebrando o próprio mecanismo de login persistente.

## Decisão
1. O handler (`RefreshAccessTokenCommandHandler`) usa `.IgnoreQueryFilters()`
   no LINQ para essa única consulta, buscando por `TokenHash` (SHA-256 de um
   segredo de 512 bits, com índice `UNIQUE`).
2. A política de RLS da tabela `refresh_tokens` tem uma cláusula adicional:
   ```sql
   USING (
       tenant_id = current_setting('app.tenant_id')::uuid
       OR current_setting('app.tenant_id')::uuid = '00000000-0000-0000-0000-000000000000'::uuid
   )
   ```
3. Assim que o registro é encontrado, o handler chama
   `tenantContext.SetTenant(presentedToken.TenantId)` — toda operação seguinte
   (revogar o token antigo, criar o novo, carregar o usuário) volta a exigir o
   match exato de tenant.

## Por que isso continua seguro
A exceção não abre uma consulta genérica: só devolve linha para quem já possui
o **hash exato** de um token de alta entropia. Não há como enumerar ou listar
refresh tokens de outro tenant através dela — é equivalente a uma tabela
`WHERE token_hash = $1`, que só responde a quem já tem o segredo.

## Consequências
- Esta é a **única** tabela/consulta do sistema com esse tipo de exceção.
  Qualquer nova tabela `ITenantOwned` deve ter política de RLS sem exceção,
  salvo justificativa equivalente registrada em ADR.
- `Agendio.IntegrationTests.RefreshTokenFlowTests` cobre o fluxo completo,
  incluindo detecção de reuso (token já revogado revoga a família inteira).
