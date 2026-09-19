# 0010 — Portal do cliente sem senha e separado das contas da equipe

## Status
Aceito.

## Contexto
A página pública permitia agendar sem login e criava/reutilizava o cadastro de
`Customer` pelo e-mail, mas o cliente não tinha uma área para consultar seus
próximos horários e pontos. O login existente pertence ao painel do estabelecimento:
seus usuários são `Owner` ou `Staff` e recebem JWT com a claim `tenant_id`.

Transformar cada cliente em um `Identity.User` acrescentaria senha e confirmação
antes do primeiro agendamento, misturaria duas autoridades diferentes e aumentaria
o atrito justamente no principal fluxo público do produto.

## Decisão

1. O primeiro agendamento continua anônimo. Nome e e-mail criam ou reutilizam o
   `Customer` dentro do tenant, como já acontecia.
2. “Minha conta” usa um código numérico de seis dígitos enviado ao e-mail já
   cadastrado. A resposta do pedido é sempre `204`, exista ou não o e-mail, para
   evitar enumeração de clientes.
3. O código expira em dez minutos, aceita no máximo cinco tentativas e é consumido
   no primeiro uso. Redis guarda apenas seu hash SHA-256; o código em texto puro
   existe somente durante o envio do e-mail.
4. A sessão do portal é um token opaco de 256 bits em cookie `HttpOnly`,
   `SameSite=Lax`, restrito ao path da API daquele tenant. Redis armazena somente
   o hash do token, associado a `{TenantId, CustomerId}`, com validade de sete dias.
5. Cliente não ganha papel em `Identity.User`. A autenticação do portal permanece
   no módulo Customers; `Owner`/`Staff` continuam sendo exclusivamente contas da
   equipe.
6. Customers lê os agendamentos pela interface
   `ICustomerPortalAppointmentsLookupService` de `Scheduling.Contracts`. Nenhum
   módulo lê diretamente a tabela de outro módulo.

## Consequências

- O cliente acessa próximos horários, histórico e fidelidade sem criar ou lembrar
  senha.
- Uma conta de cliente vale somente para o estabelecimento em que o e-mail foi
  cadastrado; a chave de sessão inclui o `TenantId` e não atravessa tenants.
- Redis e e-mail transacional tornam-se dependências do acesso ao portal. O
  agendamento público continua funcionando mesmo se uma delas estiver indisponível.
- Cancelamento e remarcação pelo cliente ficam para uma evolução posterior. Antes
  disso será necessário definir política de antecedência, cobrança/sinal e proteção
  CSRF para comandos autenticados por cookie.
- `CustomerPortalTests` cobre o fluxo real de e-mail e código usando MailHog e prova
  que uma sessão do tenant A é rejeitada no tenant B.
