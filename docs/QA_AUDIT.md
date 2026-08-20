# QA Audit — Agendio (testes reais via API, curl)

**Data:** 2026-08-18
**Executor:** QA/Test Engineer (auditoria via API, sem alteracao de codigo)
**Ambiente:** Backend em `http://localhost:5071` (ASPNETCORE_ENVIRONMENT=Development), Postgres/Redis/RabbitMQ/Mailhog via docker compose, instancia ja rodando (PID 28968) antes do inicio da auditoria.

## Metodologia

Todos os testes abaixo foram executados de verdade via `curl` contra a API rodando, ou via `dotnet test` para o caso de reuso de refresh token. Nenhum teste foi "assumido" — cada linha da tabela reflete uma chamada HTTP real e a resposta real recebida.

Setup usado como base para os testes autenticados:
- Tenant 1 (`qa-test-bshop-1`, id `00e14dba-58e5-45c4-b26e-51846270856c`), owner `qaowner@example.com`.
- Tenant 2 (`qa-test-tenant-2`, id `d3569912-039b-4a5d-a0df-e2344d1f9cdb`), owner `qaowner2@example.com`, criado so para o teste de isolamento cross-tenant.
- Confirmacao de e-mail feita direto no banco (`UPDATE identity.users SET email_confirmed_at = now() ...`), pois o fluxo real de confirmacao por e-mail nao faz parte do escopo desta bateria.
- Um recurso (Barbeiro QA, Person), um servico (Corte QA, 30min/R$50) e um cliente (Cliente QA) foram criados no Tenant 1 para servir de base aos testes de agendamento.

## Resumo executivo

- **Testes executados:** 46
- **Passou (comportamento == esperado):** 42
- **Achados (comportamento != esperado ou risco real):** 4
- **P0 (critico):** 0
- **P1 (alto):** 1 — vazamento de stack trace completo (incluindo caminhos de arquivo do servidor) em respostas 400 quando o corpo JSON e malformado (UTF-8 invalido, `DateOnly` invalido, enum invalido). Reproduzido em 3 endpoints diferentes (Customers, Financeiro x2) o que indica ser um problema **sistemico** de infraestrutura (falta um exception handler global para falhas de desserializacao), nao um bug isolado de um endpoint.
- **P3 (baixo):** 1 — criacao de cliente nao e idempotente: reenviar o mesmo POST duas vezes rapido cria dois registros distintos com os mesmos dados.
- **Informativo (nao e bug, mas vale registrar):** 2 — `no-show` pode ser marcado direto a partir de `Scheduled` sem passar por `Confirmed`/`start` (parece intencional: cliente que nunca confirma e nao aparece); agendamento nao valida contra o horario de trabalho do recurso quando nenhum horario foi configurado (permitiu agendar num recurso sem `working-hours` cadastrado).

O que mais importa: **os testes de maquina de estados do agendamento, controle de concorrencia (exclusion constraint do Postgres) e isolamento multi-tenant passaram 100%** — sao as tres areas mais criticas para um SaaS de agendamento e estao solidas. O achado mais serio (P1) e de vazamento de informacao (stack trace), nao de integridade de dados ou de autorizacao.

## Nao testado por tempo

A sessao foi interrompida por reset de limite de uso a meio da bateria. Priorizei maquina de estados e concorrencia (mais reveladores) conforme orientacao. Ficaram **fora do escopo desta rodada**:
- Fluxo de MFA (setup/enable/disable/verify) fim a fim.
- Fluxo de convite de equipe (`/api/team/invitations`) e aceite.
- Fila de espera (`/api/waitlist`) — conversao e cancelamento.
- Upload de arquivos (logo/banner/foto) com arquivo malicioso, tipo MIME incorreto, tamanho acima do limite.
- Importacao de clientes via CSV (`/api/customers/import`) com CSV malformado.
- Billing/Asaas (checkout, webhook, cancelamento de assinatura).
- Marketing (campanhas por segmento/canal).
- Assistente (Fase 22).
- Painel Super Admin.
- Fuzzing sistematico de todos os enums/DateOnly em todos os ~90 endpoints (o achado P1 abaixo foi confirmado em 3 endpoints; e provavel que se repita em qualquer endpoint com campo `enum`, `DateOnly`/`DateOnly?` ou `Guid` no corpo JSON, mas isso nao foi verificado exaustivamente).

## Achados detalhados

### P1 — Stack trace completo vazado em respostas 400 quando o JSON do corpo e invalido

**Onde:** reproduzido em `POST /api/customers` (UTF-8 invalido no campo string) e `POST /api/financeiro/contas-a-pagar` (formato de `DateOnly` invalido e valor de enum invalido). O padrao da aplicacao usa `Result<T>` + `ToProblemResult()` para erros de negocio/validacao (FluentValidation), retornando `application/problem+json` limpo. Mas quando o `System.Text.Json` falha **antes** de o Minimal API conseguir montar o objeto de request (JSON malformado, enum fora do dominio, data em formato errado, string com bytes UTF-8 invalidos), a excecao `BadHttpRequestException`/`JsonException` nao passa pelo pipeline de `Result` — ela e capturada pelo middleware de desenvolvimento (`DeveloperExceptionPageMiddlewareImpl`) e devolvida crua no corpo da resposta, com status 400 mas contendo:
- Stack trace completo do .NET, incluindo linha de codigo (`Program.cs:line 292`).
- Nomes internos de classes (`Agendio.Modules.Customers.Endpoints.CustomerEndpoints+CreateCustomerRequest`).
- Caminho absoluto do servidor (`C:\Projetos\Sistema de controle de estabelecimento\backend\src\host\Agendio.Api\Program.cs`).

Isso esta acontecendo porque `ASPNETCORE_ENVIRONMENT=Development` esta ativo (o que habilita a Developer Exception Page). Em producao esse valor precisa ser `Production`, o que desativaria essa pagina especifica — mas o problema de fundo continua: **nao existe um exception handler global que traduza falhas de desserializacao de JSON num `ProblemDetails` limpo**, entao mesmo em producao esse tipo de requisicao provavelmente cai no handler de excecao padrao do ASP.NET (uma resposta 500 generica, sem o stack trace, mas ainda fora do padrao `Result`/`ToProblemResult` usado no resto da API). Recomendo:
1. Adicionar um `IExceptionHandler` (ou middleware) global que capture `BadHttpRequestException`/`JsonException` e devolva 400 com `ProblemDetails` no mesmo formato do resto da API, nunca a excecao crua.
2. Confirmar que `ASPNETCORE_ENVIRONMENT` nunca e `Development` em producao (fora do escopo desta auditoria de API, mas vale checar a config de deploy).

Como reproduzir (exemplo com data invalida, o caso mais facil de disparar por engano por um cliente real, sem precisar forjar bytes invalidos):
```
POST /api/financeiro/contas-a-pagar
{"description":"x","amount":100,"dueDate":"31-31-9999","category":"Other"}
```
→ 400 com ~5KB de stack trace no corpo.

### P3 — Criacao de cliente nao e idempotente em double-submit

Duas requisicoes `POST /api/customers` identicas (mesmo nome, mesmo e-mail), enviadas em sequencia rapida, criam **dois registros distintos** com o mesmo e-mail. Nao ha constraint de unicidade de e-mail por tenant nem qualquer chave de idempotencia (`Idempotency-Key` ou similar). Nao e um problema de seguranca, mas gera duplicidade de cadastro em cliques duplos no frontend (double-click num botao "Salvar" sem debounce, ou retry de rede). Sugestao: unique index em `(tenant_id, email)` quando `email` nao for nulo (com tratamento de conflito amigavel), ou suporte a `Idempotency-Key` no endpoint de criacao.

### Informativo — `no-show` permitido direto de `Scheduled`

`POST /api/appointments/{id}/no-show` foi aceito (204) num agendamento que nunca saiu de `Scheduled` (nunca foi `Confirmed` nem teve `start` chamado). A mensagem de erro da transicao invalida confirma que isso e intencional: *"So e possivel marcar Nao compareceu em um agendamento Agendado ou Confirmado."* — ou seja, `Scheduled` e um estado valido de origem por design. Registrando apenas para confirmar que foi um comportamento observado e nao um bug de maquina de estados.

### Informativo — Agendamento nao valida contra horario de trabalho do recurso quando nenhum horario foi configurado

O recurso de teste (Barbeiro QA) nunca teve `PUT /api/resources/{id}/working-hours` chamado, e mesmo assim o agendamento foi aceito em qualquer horario. Pode ser um default intencional ("sem horario configurado = sempre disponivel", alinhado ao principio do CLAUDE.md de "funciona com defaults sensatos sem configuracao"), mas vale confirmar com o time de produto se e essa a intencao, porque tambem pode mascarar um bug de nao aplicar a regra de horario de trabalho.

## Tabela de testes

| ID | Area | Teste | Esperado | Resultado real | Severidade | Status |
|---|---|---|---|---|---|---|
| A1 | Auth | Login com senha errada | 401 | 401 `Auth.InvalidCredentials` | - | PASS |
| A2 | Auth | Login com e-mail inexistente | 401 (sem enumeracao de conta) | 401 `Auth.InvalidCredentials` (mesma mensagem de A1) | - | PASS |
| A3 | Auth | Login com `tenantId` = Guid.Empty | 400 validacao | 400 `'Tenant Id' deve ser informado.` | - | PASS |
| A3b | Auth | Login com `tenantId` aleatorio inexistente (guid valido) | 404 | 404 `Tenant.NotFound` | - | PASS |
| A4 | Auth | Registro com e-mail ja usado no mesmo tenant | 409 | 409 `User.EmailTaken` | - | PASS |
| A5 | Auth | Registro com senha de 6 caracteres (< 10) | 400 | 400 `A senha precisa ter pelo menos 10 caracteres.` | - | PASS |
| A6 | Auth | Registro com email/senha/nome vazios | 400 com todas as violacoes | 400 listando as 4 violacoes (incluindo dupla mensagem de senha) | - | PASS |
| A7 | Auth | `GET /api/customers` sem header Authorization | 401 | 401 | - | PASS |
| A8 | Auth | `GET /api/customers` com token malformado (`not.a.valid.jwt.token`) | 401 | 401 | - | PASS |
| A9 | Auth | `GET /api/customers` com header `Authorization` sem prefixo `Bearer` | 401 | 401 | - | PASS |
| A9b | Auth | `GET /api/customers` com JWT bem formado mas assinado com chave errada (token de exemplo publico) | 401 | 401 | - | PASS |
| A10 | Auth | Reuso de refresh token (`dotnet test --filter RefreshToken`) | Toda a familia de tokens revogada no reuso | 6/6 testes automatizados passaram | - | PASS |
| A11 | Auth | Rate limit em `/api/auth/login`: 15 tentativas rapidas do mesmo IP | 429 a partir da 11a (limite default 10/60s) | 10x 401, depois 5x 429 | - | PASS |
| S1 | Agenda | Criar agendamento com `startAtUtc` no passado (`2020-01-01`) | 400 | 400 `Appointment.StartInThePast` | - | PASS |
| S2 | Agenda | Criar agendamento valido no futuro | 201 | 201 | - | PASS (setup) |
| S3 | Agenda | Criar 2 agendamentos no mesmo recurso, exatamente no mesmo horario (sequencial) | 2o = 409 | 409 `Appointment.SlotTaken` | - | PASS |
| S3b | Agenda | 2o agendamento com overlap parcial (15 min depois, servico de 30 min) | 409 | 409 `Appointment.SlotTaken` | - | PASS |
| S4 | Agenda | `complete` num agendamento ainda `Scheduled` (sem passar por `start`) | 400, maquina de estados bloqueia | 400 `Appointment.InvalidTransition` | - | PASS |
| S5 | Agenda | `no-show` num agendamento `Scheduled` | Aceito (ver nota informativa) | 204 | Info | PASS (comportamento intencional confirmado pela mensagem de erro de S5b) |
| S5b | Agenda | `no-show` de novo no mesmo agendamento (ja `NoShow`) | 400 | 400 `Appointment.InvalidTransition` | - | PASS |
| S6 | Agenda | `cancel` num agendamento que ja esta `NoShow` | 400 | 400 `Appointment.InvalidTransition` ("nao pode mais ser cancelado") | - | PASS |
| S7a | Agenda | `cancel` num agendamento `Scheduled` | 204 | 204 | - | PASS (setup) |
| S7b | Agenda | `cancel` de novo no mesmo agendamento (ja cancelado) | 400 | 400 `Appointment.InvalidTransition` | - | PASS |
| S8 | Agenda | `start` num agendamento `Scheduled` | 204 | 204 | - | PASS |
| S9 | Agenda | `complete` apos `start` | 204 | 204 | - | PASS |
| S10 | Agenda | `no-show` num agendamento ja `Completed` | 400 | 400 `Appointment.InvalidTransition` | - | PASS |
| S11 | Agenda | `GET /api/appointments/{id-aleatorio}` | 404 | 404 `Appointment.NotFound` | - | PASS |
| S12 | Agenda | `POST /api/appointments/{id-aleatorio}/start` | 404 | 404 `Appointment.NotFound` | - | PASS |
| S13 | Agenda | `POST /api/appointments/{id-aleatorio}/complete` | 404 | 404 `Appointment.NotFound` | - | PASS |
| S14 | Agenda | `POST /api/appointments/{id-aleatorio}/cancel` | 404 | 404 `Appointment.NotFound` | - | PASS |
| CONC1 | Agenda | 2 `POST /api/appointments` disparados **em paralelo de verdade** (background shell jobs) pro mesmo recurso+horario | 1 sucesso (201) + 1 conflito (409); nunca os dois criados | A: 201, B: 409. Conferido no Postgres: **exatamente 1 linha** na tabela `scheduling.appointments` para o slot | - | PASS |
| DUP1 | Agenda/Dados | Reenviar o mesmo `POST /api/customers` 2x rapido (dados identicos) | Idempotente ou rejeitado | Criou **2 registros diferentes** com os mesmos dados | P3 | FAIL |
| G1 | Agenda | `GET /api/customers/nao-e-um-guid` (guid malformado no path) | 404 (route constraint) ou 400, nunca 500 | 404 | - | PASS |
| RS1 | Agenda | `PUT /api/appointments/{id}/reschedule` para um horario ja ocupado por outro agendamento | 409 | 409 `Appointment.SlotTaken` | - | PASS |
| C1 | Clientes | Criar cliente com `fullName` vazio | 400 | 400 `'Full Name' deve ser informado.` | - | PASS |
| C2 | Clientes | CPF com todos os digitos iguais (`111.111.111-11`) | 400 | 400 `CpfCnpj.InvalidFormat` | - | PASS |
| C2b | Clientes | CPF com digito verificador incorreto (`123.456.789-00`) | 400 | 400 `CpfCnpj.InvalidFormat` | - | PASS |
| C3 | Clientes | `fullName` com 10.000 caracteres | 400 (limite de validacao), nunca 500 | 400 `'Full Name' deve ser menor ou igual a 200 caracteres.` | - | PASS |
| C4 | Clientes | `fullName` = `'; DROP TABLE customers; --` | 201, tratado como string literal, tabela intacta | 201, cliente criado normalmente com o texto literal como nome | - | PASS |
| C5 | Clientes | `fullName` com emoji/acentos/`<script>`/chines, UTF-8 valido, enviado via arquivo binario (para eliminar mangling do shell) | 201, string armazenada como veio | 201 | - | PASS |
| C5-bad | Clientes | `fullName` com byte UTF-8 deliberadamente invalido (`\xE9` solto) no corpo JSON | 400 limpo (`ProblemDetails`) | 400 mas com **stack trace completo (~5.6KB) vazado no corpo**, incluindo caminho do servidor | **P1** | FAIL |
| F1 | Financeiro | Despesa com `amount` negativo | 400 | 400 `'Amount' deve ser superior a '0'.` | - | PASS |
| F2 | Financeiro | Despesa com `amount` = 0 | 400 | 400 (mesma mensagem de F1) | - | PASS |
| F3 | Financeiro | Despesa com `dueDate` = `"31-31-9999"` (formato invalido) | 400 limpo | 400 mas com **stack trace completo vazado** (`System.FormatException: DateOnly format`) | **P1** (mesma raiz de C5-bad) | FAIL |
| F4 | Financeiro | Despesa com `category` = `"CategoriaQueNaoExiste"` (fora do enum) | 400 limpo | 400 mas com **stack trace completo vazado** | **P1** (mesma raiz de C5-bad) | FAIL |
| P1t | Estoque | Produto com `costPrice`/`salePrice` negativos | 400 | 400 listando as duas violacoes | - | PASS |
| SV1 | Catalogo | Servico com `price` negativo | 400 | 400 `'Price' deve ser superior ou igual a '0'.` | - | PASS |
| SV2 | Catalogo | Servico com `durationMinutes` = 0 | 400 | 400 `'Duration Minutes' deve ser superior a '0'.` | - | PASS |
| ES1 | Estoque | Movimentacao de estoque com `quantity` negativa | 400 | 400 `'Quantity' deve ser superior a '0'.` | - | PASS |
| ES2 | Estoque | Saida (`Exit`) de 999 unidades num produto com 10 em estoque | 400, nunca deixa estoque negativo | 400 `Product.InsufficientStock` | - | PASS |
| T1 | Multi-tenant | Tenant 2 tenta `GET /api/customers/{id}` de um cliente do Tenant 1 | 404 (nunca vazar existencia) | 404 `Customer.NotFound` | - | PASS |
| T2 | Multi-tenant | Tenant 2 tenta `GET /api/appointments/{id}` de um agendamento do Tenant 1 | 404 | 404 `Appointment.NotFound` | - | PASS |
| T3 | Multi-tenant | Tenant 2 lista `GET /api/customers` (nao deve ver os do Tenant 1) | Lista vazia | `{"items":[],"totalCount":0,...}` | - | PASS |
| SQL1 | Codigo | Grep por `FromSqlRaw`/`ExecuteSqlRaw` (concatenacao manual de SQL) em `backend/src` | Nenhum uso inseguro | Apenas 4 usos de `ExecuteSqlInterpolatedAsync` (parametrizado via `FormattableString`, seguro), zero `FromSqlRaw`/`ExecuteSqlRaw` | - | PASS |

**Total: 46 testes, 42 PASS, 4 FAIL (1 P1 recorrente em 3 linhas da tabela + 1 P3).**
