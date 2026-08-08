# 0006 — UseRateLimiter entre UseAuthentication e UseAuthorization

## Status
Aceito (Sprint 8).

## Contexto
O rate limit global precisava passar a particionar por tenant (claim `tenant_id`
do JWT) em vez de só por IP, para que um tenant abusivo não consumisse a cota
de todos os outros. Isso exige que `HttpContext.User` já esteja populado com
as claims no momento em que a partition key é calculada — e só
`UseAuthentication()` faz isso.

## Decisão
```csharp
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
```
`UseRateLimiter()` fica **depois** de `UseAuthentication()` (para ler a claim
`tenant_id` quando há um JWT válido) e **antes** de `UseAuthorization()`
(não depois dos dois).

A ordem importa nos dois sentidos:
- Se `UseRateLimiter()` ficasse **antes** de `UseAuthentication()` (como estava
  até este sprint), a claim nunca estaria disponível — toda requisição
  autenticada cairia no fallback por IP, e o particionamento por tenant nunca
  funcionaria.
- Se `UseRateLimiter()` ficasse **depois** de `UseAuthorization()`, um flood de
  requisições sem token válido (ou com token forjado) contra uma rota protegida
  seria rejeitado com 401 por `UseAuthorization()` antes de sequer chegar ao
  limiter — nenhuma dessas requisições consumiria cota nenhuma, reabrindo
  exatamente o tipo de gap de DoS que o rate limiting existe para fechar.

`UseAuthentication()` nunca rejeita uma requisição por si só (token
ausente/inválido vira principal anônimo) — é isso que permite o limiter, entre
os dois, servir tanto o caminho autenticado (partition por tenant) quanto o
anônimo (fallback por IP) sem abrir brecha nenhuma.

## Consequências
- Ordem não óbvia — fácil de "simplificar" movendo o limiter de volta para
  antes de tudo (parece mais seguro à primeira vista) e reintroduzir o gap.
  Qualquer PR que mexer na ordem do pipeline em `Program.cs` deve preservar
  isso ou atualizar esta ADR com a nova justificativa.
- `Agendio.IntegrationTests.RateLimitingTests` cobre o particionamento por
  tenant e a política `"auth"` aplicada em login/registro do tenant (que antes
  deste sprint não tinha rate limit nenhum além do global).
