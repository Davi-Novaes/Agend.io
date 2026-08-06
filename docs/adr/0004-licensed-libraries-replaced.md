# 0004 — Substituição de MediatR, AutoMapper e FluentAssertions

## Status
Aceito (Sprint 0).

## Contexto
MediatR, AutoMapper e FluentAssertions (v8+) passaram a exigir licença
comercial para uso corporativo. Um projeto que pretende crescer para milhares
de empresas não deveria acoplar sua arquitetura central a uma dependência com
risco de custo/licenciamento variável.

## Decisão
| Substituído | Escolha | Detalhe |
|---|---|---|
| MediatR | Dispatcher próprio (`Agendio.SharedKernel.Messaging`) | `ICommand`/`IQuery`/`IPipelineBehavior` + `Dispatcher` (~100 linhas), registrado via `AddDispatcher()`/`AddHandlersFromAssembly()`. |
| AutoMapper | Mapeamento manual ou Mapperly (source generator) | Mapeamento em compile-time, sem reflection; erro de mapeamento vira erro de build. |
| FluentAssertions | Shouldly | API de assertions fluente equivalente, licença Apache 2.0/BSD sem restrição comercial. |

## Consequências
- CQRS continua com a mesma forma de uso (`dispatcher.Send(command)`), sem
  vazar a troca de biblioteca para o código de aplicação.
- Pipeline de comportamentos (`LoggingBehavior`, `ValidationBehavior`,
  `ExplicitTenantBehavior`) é registrado como generic aberto, aplicado
  automaticamente a todo handler de todo módulo.
- Antes de adicionar qualquer pacote novo ao projeto, checar a licença
  explicitamente (ver CLAUDE.md).
