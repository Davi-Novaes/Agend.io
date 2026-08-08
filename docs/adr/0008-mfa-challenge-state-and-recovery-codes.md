# 0008 — Estado do desafio de MFA em Redis e códigos de recuperação em tabela dedicada

## Status
Aceito (Sprint 8).

## Contexto
MFA/TOTP foi deliberadamente adiado do MVP (comentário em `Program.cs`, Sprint
6). Login em duas etapas — senha confirma, código TOTP confirma — precisa de um
estado intermediário entre as duas chamadas: "esta senha já validou para este
usuário, mas o token final ainda não foi emitido". Esse estado precisa expirar
sozinho (5 min) e nunca pode ser reapresentado com sucesso duas vezes.

## Decisão

**Desafio de MFA: token opaco de alta entropia em Redis, não um JWT.**
`LoginCommandHandler` gera o token com `IRefreshTokenGenerator.GenerateToken()`
(mesma primitiva de alta entropia já usada para refresh tokens) e grava
`{UserId, TenantId}` em `IDistributedCache` (`RedisMfaChallengeStore`) com TTL
de 5 minutos. `VerifyMfaCommandHandler` consome (lê e apaga imediatamente) essa
entrada — reapresentar o mesmo token depois de um `ConsumeAsync` bem-sucedido
(ou mal-sucedido) sempre falha. Um JWT stateless foi descartado porque precisa
de revogação no primeiro uso, o que exigiria um denylist adicional só para essa
finalidade — Redis já é dependência obrigatória desta API (o host nem sobe sem
ele, ver `IntegrationTestFixture` e `AddAgendioInfrastructure.AddRedis`), então
usá-lo aqui não introduz uma dependência nova.

**Códigos de recuperação: tabela `mfa_recovery_codes`, não array jsonb no `User`.**
Mesmo molde de `RefreshToken` — entidade `ITenantOwned` própria, 1:muitos com
`User`, hash SHA-256 em hex (reaproveitando `IRefreshTokenGenerator.Hash`,
nunca o código em texto puro persistido). RLS padrão, sem a exceção usada em
`refresh_tokens`/`team_invitations`: a consulta a um código de recuperação só
acontece depois que `tenantContext.SetTenant(...)` já ancorou o tenant (o
`VerifyMfaCommandHandler` resolve o challenge no Redis primeiro, que já traz o
`TenantId`), então não existe momento em que a busca precise rodar com
`app.tenant_id` no sentinela vazio. Tabela dedicada em vez de array jsonb
evita race de leitura-modificação-escrita quando 10 códigos são gerados de uma
vez (`EnableMfaCommandHandler`) ou um é consumido (`MfaCodeVerifier`), e ganha
RLS/auditoria de graça — a mesma garantia que qualquer outra tabela do sistema
já tem, sem código especial.

## Consequências
- Uma falha do Redis bloqueia login de usuários com MFA habilitado (sem
  fallback). Aceitável: Redis já é dependência dura da API hoje, isso não é
  uma fragilidade nova introduzida pelo MFA.
- Os 10 códigos de recuperação só aparecem em texto puro na resposta de
  `POST /api/auth/mfa/enable` — nenhuma tela ou log volta a mostrá-los depois
  disso; perder todos exige desabilitar e reabilitar MFA (`DisableMfaCommand`
  apaga os códigos restantes).
- `Agendio.IntegrationTests.MfaTests` cobre o ciclo completo — desafio em vez
  de tokens, cookie de refresh não setado no desafio, uso único do challenge
  token, código TOTP e de recuperação, segredo persistido criptografado (ver
  ADR 0007), rate limit no `/mfa/verify`, e o teste de isolamento cruzado
  obrigatório (CLAUDE.md): conecta como a role de runtime (`agendio_app`,
  `NOBYPASSRLS`) ancorada no tenant B e confirma que a política de RLS —
  não só o Global Query Filter do EF Core — impede a leitura de um código de
  recuperação do tenant A pelo Id exato.
