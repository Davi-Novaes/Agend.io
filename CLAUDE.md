# Agendio — Convenções do Projeto

Plataforma SaaS multi-tenant de gestão para negócios baseados em agendamento
(barbearia, clínica, psicólogo, pet shop, lava-rápido, advogado, personal…).

> `Agendio` é nome de trabalho. Trocar = find/replace em `backend/` e `frontend/`.

## Idioma

- **Código, banco de dados, nomes de API, commits:** inglês.
- **UI, mensagens de erro para o usuário, documentação, comentários:** português (pt-BR).
- Comentários explicam **por quê**, nunca **o quê**. Código óbvio não leva comentário.

## Estrutura

```
backend/
  src/host/Agendio.Api            # composition root, Minimal APIs
  src/shared/Agendio.SharedKernel # blocos de construção do domínio, sem dependências
  src/shared/Agendio.Infrastructure
  src/modules/<Modulo>/           # Domain/ Application/ Infrastructure/ Endpoints/
  src/modules/<Modulo>.Contracts/ # superfície pública do módulo
  tests/
frontend/                         # Next.js App Router
infra/                            # docker-compose, scripts de banco
docs/adr/                         # decisões arquiteturais registradas
_legacy/                          # projeto Python sem relação, preservado
```

## Regras de arquitetura (verificadas por teste em CI — não são sugestão)

1. `Domain/` não referencia `Application/`, `Infrastructure/` nem pacote externo além do SharedKernel.
2. Um módulo **nunca** referencia outro módulo — apenas `<Outro>.Contracts`.
3. Comunicação entre módulos: integration event (RabbitMQ, assíncrono) ou interface pública em `.Contracts` (leitura, síncrono).
4. Nenhum módulo lê tabela de outro módulo.
5. Toda entidade `ITenantOwned` tem global query filter por `TenantId` **e** RLS habilitada na tabela.

Quebrar qualquer uma dessas quebra o build. É intencional.

## Multi-tenancy — defesa em profundidade

Duas camadas independentes, verificadas por teste de isolamento cruzado real (não só leitura de
código). Nunca remova uma "porque a outra já cobre".

1. Global query filter do EF Core, por `TenantId` resolvido da claim `tenant_id` do JWT (ver
   `HttpTenantContext`).
2. Row Level Security no PostgreSQL — a aplicação conecta com role **sem `BYPASSRLS`**.

> Uma auditoria de segurança (2026-08, ver `docs/BACKEND_AUDIT.md` achado P1-2 e
> `docs/BACKLOG.md` BL-06) encontrou uma "Camada 1" documentada aqui anteriormente — resolução de
> tenant por subdomínio/slug com checagem de divergência contra o JWT — que **nunca existia no
> código**. A arquitetura real de resolução de tenant é: o cliente informa o tenant explicitamente
> (slug/Guid) no login/cadastro, e daí em diante todo acesso autenticado usa só a claim `tenant_id`
> do JWT — não há roteamento por subdomínio em nenhuma camada (a página pública usa path, `/slug`,
> não subdomínio). Se subdomínio-por-tenant for implementado no futuro, documentar aqui só depois
> do middleware existir de verdade — não descrever proteção que não está implementada.

**Toda** feature nova que toca dados de tenant precisa de um teste de isolamento cruzado.

## Padrões de código

- CQRS com dispatcher próprio: `ICommandHandler<TCommand, TResult>` / `IQueryHandler<TQuery, TResult>`.
- Retorno de handler é `Result<T>` — exceção é para o excepcional, não para regra de negócio violada.
- Validação com FluentValidation em pipeline behavior, nunca dentro do handler.
- Mapeamento com **Mapperly** (source generator) ou manual. **Não usar AutoMapper.**
- Ids fortemente tipados (`AppointmentId`, `TenantId`) — impede passar um Guid no lugar do outro.
- Datas: `timestamptz` em UTC no banco, `IanaTimeZone` no tenant, conversão via **NodaTime**.
  **Nunca** `DateTime.Now` / `DateTime.UtcNow` direto — sempre `IClock`.
- Repository apenas onde há query complexa a encapsular. `DbContext` direto no resto.

## Bibliotecas deliberadamente evitadas

| Evitado | Motivo | Usar |
|---|---|---|
| MediatR | licença comercial | dispatcher próprio no SharedKernel |
| AutoMapper | licença comercial | Mapperly |
| FluentAssertions v8+ | licença comercial | Shouldly |

Antes de adicionar qualquer pacote novo, verifique a licença.

## Segurança — inegociável

- Senha: Argon2id. Nunca outro algoritmo.
- Access token JWT de 15 min, **em memória** no frontend. Refresh token rotativo em cookie `HttpOnly; Secure; SameSite=Lax`, guardado **hasheado**. Reuso de refresh revoga a família inteira.
- Super Admin é autoridade separada (`scope: platform`), nunca um papel dentro de tenant.
- Dado sensível (CPF, saúde) criptografado em coluna.
- Nunca logar: senha, token, CPF, dado de saúde. O enricher do Serilog redige — não confie só nele.

## Testes

- `dotnet test` precisa passar antes de qualquer commit.
- Unitário: xUnit v3 + Shouldly. Integração: Testcontainers (Postgres/Redis reais).
- Nome do teste descreve comportamento: `Should_Reject_Booking_When_Slot_Already_Taken`.
- Bug corrigido = teste que falha antes da correção.

## Acessibilidade (WCAG 2.2 AA) — requisito, não melhoria

- axe-core roda no Playwright como gate de CI.
- Paleta de tema personalizada é **rejeitada ao salvar** se não atingir contraste AA.
- Todo fluxo precisa ser completável só com teclado.

## Comandos

```bash
docker compose -f infra/docker-compose.yml up -d   # infra local
dotnet build backend/Agendio.sln
dotnet test backend/Agendio.sln
dotnet run --project backend/src/host/Agendio.Api
cd frontend && npm run dev
```

## Fluxo de trabalho por feature

Requisitos → regras de negócio → casos de uso → modelo de domínio → modelo de dados →
arquitetura → contrato de API → segurança → testes → código → revisão → melhorias.

Decisão arquitetural relevante vira uma ADR em `docs/adr/`.

## Princípio norteador

O produto atende qualquer segmento **sem ser difícil de configurar**. Toda feature nova
responde: "isso funciona com defaults sensatos, sem o dono do negócio configurar nada?"
Se a resposta for não, a feature ainda não está pronta.
