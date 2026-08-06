# 0003 — Um DbContext por módulo, não um DbContext único

## Status
Aceito (Sprint 0).

## Contexto
A regra de arquitetura "nenhum módulo acessa tabela de outro módulo" só é
verificável de verdade se cada módulo literalmente não enxergar o `DbSet` do
outro. Um `DbContext` único conhecendo entidades de todos os módulos exigiria
que `Agendio.Infrastructure` (referenciada por todos) tivesse referência de
volta para cada módulo — dependência circular impossível com a direção de
dependência que adotamos (módulo → Infrastructure, nunca o contrário).

## Decisão
Cada módulo declara o próprio `DbContext` (`TenancyDbContext`,
`IdentityDbContext`, ...), derivado de `AgendioDbContextBase`
(`Agendio.Infrastructure`), com o próprio schema PostgreSQL (`tenancy`,
`identity`, ...) e a própria tabela de outbox. Todos apontam para o **mesmo
banco físico** — só o modelo do EF Core é particionado por módulo, não a
infraestrutura.

Cada módulo tem também a própria `IDesignTimeDbContextFactory`, usada
exclusivamente pela ferramenta `dotnet ef`, conectando como `agendio_owner`
(ver ADR 0001).

## Consequências
- Migrations são por módulo (`dotnet ef migrations add --project
  src/modules/X/... --context XDbContext`), com histórico de migration próprio.
- Nenhuma consulta cross-módulo é possível via EF por construção — força o uso
  de `X.Contracts` (interface pública) para leitura síncrona entre módulos,
  como `ITenantLookupService`.
- Ao extrair um módulo para microsserviço no futuro, o `DbContext` dele já é
  autocontido — não precisa "desembaraçar" tabelas de um contexto compartilhado.
