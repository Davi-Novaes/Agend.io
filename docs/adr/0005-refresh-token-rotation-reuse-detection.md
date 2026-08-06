# 0005 — Rotação de refresh token com detecção de reuso

## Status
Aceito (Sprint 0).

## Contexto
Refresh tokens de vida longa (30 dias) são um alvo valioso se vazarem (log,
extensão de navegador maliciosa, XSS). Um token estático reutilizável
indefinidamente amplifica o dano de qualquer vazamento.

## Decisão
- Cada login gera um `RefreshToken` com um `FamilyId` novo (`Guid`).
- Cada uso do refresh **consome** o token atual e emite um novo na mesma
  família (`RefreshToken.Rotate`), marcando o anterior como revogado.
- Se um token **já revogado** for apresentado de novo — só acontece se alguém
  capturou um token que já foi trocado, sinal de roubo/replay — a **família
  inteira** é revogada (`RevokeEntireFamilyAsync`), forçando novo login em
  todos os dispositivos daquela sessão.
- O token em si nunca é armazenado em texto plano: só o hash SHA-256
  (`IRefreshTokenGenerator.Hash`) fica no banco.
- O token trafega **somente** em cookie `HttpOnly; Secure; SameSite=Lax`,
  restrito ao path `/api/auth` — nunca no corpo da resposta JSON, nunca
  acessível a JavaScript no navegador.

## Consequências
- Um único token vazado tem janela de uso limitada: assim que o dono legítimo
  fizer o próximo refresh (ou o atacante fizer, entregando o alarme), a
  próxima tentativa do outro lado já falha e revoga a sessão inteira.
- Custo: uma escrita extra no banco por refresh (revogar + inserir), aceitável
  dado que refresh acontece na ordem de minutos, não por requisição.
- Coberto por `Agendio.IntegrationTests.RefreshTokenFlowTests` contra
  PostgreSQL real via Testcontainers.
