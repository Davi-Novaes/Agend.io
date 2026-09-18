# 0009 — Subdomínio por tenant na página pública

## Status
Aceito.

## Contexto
A página pública de agendamento (sem autenticação) de cada estabelecimento era acessível só por
path: `agendiobr.com.br/barbearia-do-ze`. Pedido do dono da plataforma: cada estabelecimento
ganhar um endereço próprio e mais fácil de compartilhar, tipo `barbearia-do-ze.agendiobr.com.br`.

Isto é puramente sobre a página pública. O resto do produto (login, dashboard, todo acesso
autenticado) já resolve tenant só pela claim `tenant_id` do JWT — ver seção "Multi-tenancy" do
CLAUDE.md. Uma auditoria anterior (2026-08, `docs/BACKEND_AUDIT.md` P1-2) encontrou justamente uma
resolução-por-subdomínio *documentada mas nunca implementada*, tratada como uma camada de
segurança que não existia. Esta ADR existe em parte pra deixar claro, por escrito, o que esta
feature **não é**: não é uma segunda camada de resolução de tenant, não afeta autenticação, não
deveria ser tratada como boundary de segurança em nenhum código futuro.

## Decisão

1. **Roteamento**: `frontend/proxy.ts` (Next.js 16 — `middleware.ts` foi renomeado pra `proxy.ts`
   nesta versão, ver AGENTS.md do frontend) lê o header `Host`. Se bater com
   `<algo>.agendiobr.com.br` e `<algo>` não estiver na lista de reservados, reescreve a URL
   internamente pra `/<algo>` (path já existente, `app/(public)/[slug]/page.tsx`). Fora do domínio
   de produção (localhost, preview), a rota por path continua funcionando sem nenhuma mudança —
   isso não é substituído, só ganha uma segunda porta de entrada.
2. **DNS**: registro coringa `*.agendiobr.com.br` no Cloudflare, apontando pro mesmo Tunnel que já
   serve `agendiobr.com.br`/`www.agendiobr.com.br`. Certificado wildcard de primeiro nível é
   emitido automaticamente pelo Cloudflare Universal SSL — sem custo, sem passo manual extra.
3. **Roteamento no edge**: nada muda no `infra/Caddyfile` — ele já roteia por *path* (`/api/*` →
   backend, resto → frontend), sem matcher de `Host`, então qualquer subdomínio cai natural no
   frontend sem precisar editar a config.
4. **Chamada de API pelo browser**: em vez de sempre chamar a origem fixa de `NEXT_PUBLIC_API_URL`,
   o cliente de API (`frontend/lib/api/client.ts`) passa a usar a **mesma origem da página** quando
   o `hostname` bate com o domínio raiz ou um subdomínio dele. Isso evita abrir CORS por wildcard
   pra cada slug de tenant (que teria que incluir `AllowCredentials`, arriscando alargar o mesmo
   policy que protege o cookie de refresh token) — a chamada simplesmente vira same-origin, porque
   o Caddy já proxya `/api/*` em qualquer `Host` recebido.
5. **Slugs reservados**: nomes que colidiriam com um subdomínio da própria plataforma (`www`,
   `api`, `admin`, `mail`...) são bloqueados na criação do tenant (`ReservedSlugs.IsReserved`,
   `Agendio.Modules.Tenancy.Domain`) — é o backend que garante isso de verdade. A lista irmã em
   `frontend/proxy.ts` (`RESERVED_SUBDOMAINS`) é só pra não tentar reescrever um subdomínio que
   nunca poderia ser um slug válido; mantidas em sincronia manual, comentário cruzado nos dois
   arquivos.

## Consequências
- Página pública ganha dois endereços válidos (path e subdomínio) — nenhum é removido, o dono
  escolhe qual divulgar.
- Nenhuma mudança em autenticação, autorização ou resolução de tenant pra rotas autenticadas — a
  claim `tenant_id` do JWT continua sendo a única fonte de verdade, exatamente como antes.
- CORS do backend (`Cors:AllowedOrigins`) não precisou virar wildcard — evitado de propósito (ver
  ponto 4).
- Custo de manutenção: uma lista de nomes reservados em dois idiomas/runtimes (C# e TypeScript),
  sem jeito de compartilhar automaticamente entre backend e frontend nesta arquitetura. Aceito
  porque a lista é pequena e muda raramente.
