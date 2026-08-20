# Auditoria de UX — 4 Personas — Agendio

**Papel:** Usuário real testando o sistema pela primeira vez, do zero, no navegador.
**Ambiente:** frontend `localhost:3000`, backend `localhost:5071`, MailHog `localhost:8025`, Postgres/RabbitMQ locais.
**Método:** navegação real via Browser pane (não leitura de código). Toda ação abaixo foi de fato executada; nada foi simulado só na leitura de código, exceto onde marcado explicitamente como "não testado".

Classificação de severidade usada: **P0** = trava o usuário completamente · **P1** = alto atrito · **P2** = médio · **P3** = polimento.

---

## Persona A — Dono de barbearia, nunca usou o sistema

**Objetivo:** cadastrar o estabelecimento do zero, confirmar e-mail, escolher plano Grátis, logar, cadastrar 1 serviço e 1 profissional, criar 1 agendamento.

### Passo a passo

1. **Landing page (`/`)** — li a proposta de valor, cliquei em "Criar minha conta grátis".
2. **Onboarding passo 1/3 — Estabelecimento.** Digitei "Barbearia Corte Certo" no nome. O campo de identificador (slug) se preencheu sozinho como `barbearia-corte-certo` assim que saí do campo de nome — bom default, sem eu precisar pensar em URL. Fuso horário já veio em `America/Sao_Paulo` (detectado do navegador).
3. **Passo 2/3 — Segmento.** Selecionei "Barbearia" entre 19 opções (Salão, Clínica Odontológica, Psicologia, Pet Shop, Advocacia, etc.). Ao selecionar, apareceu uma prévia: "Cliente vira Clientes · Serviço vira Serviços · Profissional vira Profissionais" — reforça que o sistema vai se adaptar ao meu negócio.
4. **Passo 3/3 — Conta.** Nome, e-mail, senha. Cliquei em "Criar estabelecimento".
5. **Escolha de plano.** Apareceu uma tela nova (não fazia parte da contagem "3 de 3", pega de surpresa) com 2 cartões: "Grátis" e "Padrão R$99,00/mês". Escolhi Grátis.
6. **Confirme seu e-mail.** Tela dizendo "Enviamos um link de confirmação para [email]. Clique nele para ativar sua conta." Fui ao MailHog (`localhost:8025`) esperando o e-mail.

### 🛑 Travou aqui

O e-mail **nunca chegou** ao MailHog — nem na primeira tentativa (registro), nem clicando em "Reenviar e-mail" (que retornou 429 Too Many Requests, confirmando que uma tentativa de envio real ocorreu). Esperei mais de um minuto, atualizei o MailHog várias vezes e também consultei a API do MailHog direto (`GET /api/v2/messages`) — nada. Fui ao banco de dados e confirmei que o evento de domínio (`identity.outbox_messages`) foi processado em segundos, mas `identity.users.email_confirmed_at` continuava `NULL`. Ou seja: o sistema tentou enviar, processou o evento, mas o e-mail em si nunca saiu.

Como o login exige e-mail confirmado (mudança recente da Fase 23), **não havia como prosseguir pelo fluxo normal**. Para continuar testando o restante da jornada (que é o que o resto desta auditoria depende), executei manualmente `UPDATE identity.users SET email_confirmed_at = now()` direto no Postgres — algo que só é possível com acesso ao banco, nunca para um usuário real. **Isso é uma trava P0**: sem esse acesso, a Persona A ficaria parada exatamente aqui, sem entender por quê, sem nenhuma mensagem de erro na tela (a UI não indica falha nenhuma — ela assume que o e-mail foi enviado com sucesso).

### Continuação (após bypass manual)

7. **Login.** Precisa de 3 campos: identificador do estabelecimento (slug), e-mail, senha — diferente da maioria dos logins que só pedem e-mail/senha, mas faz sentido em um sistema multi-tenant por subdomínio/slug. Logou sem problemas.
8. **Painel.** Caiu direto no dashboard, com saudação "Boa noite" (hora certa) e KPIs zerados, mas com CTAs claros: "Novo cliente", "Novo agendamento", e um bloco "Agenda de hoje" convidando a criar o primeiro agendamento. Ficou óbvio o que fazer a seguir.
9. **Cadastrar serviço (`/servicos`).** Cliquei "Novo serviço", preenchi Nome ("Corte Masculino"), Duração (30 min), Preço (R$40) e salvei. Funcionou de primeira, sem exigir nenhum campo que eu não soubesse preencher.
10. **Cadastrar profissional (`/recursos`).** A página chama isso de "Recursos" (não "Profissionais", apesar da terminologia prometida no onboarding — ver achado no Product Audit). Cliquei "Novo recurso", nome "João Barbeiro", tipo "Pessoa" (já vinha selecionado por padrão). Salvo com sucesso.

### 🛑 Travou de novo (silenciosamente)

Depois de criar o profissional, fui até a Agenda achando que já dava para agendar. **Nenhum horário de trabalho existe por padrão** — teria ficado impossível agendar qualquer coisa (e a página pública mostraria "nenhum horário disponível" para sempre) se eu não tivesse percebido, olhando as ações da linha do recurso, que existe um botão separado "Horários" que abre um modal à parte, começando vazio ("Nenhum horário cadastrado ainda"). Um dono de barbearia leigo não teria motivo óbvio para procurar esse botão — ele acabou de "criar o profissional", presumiria que já está pronto para receber agendamentos. Configurei manualmente horários de segunda a sábado, 09:00–18:00, para poder continuar o teste. **P1**: falta um horário comercial padrão pré-preenchido.

O modal "Serviços" do recurso, por outro lado, acertou o default: "Sem nenhum marcado, o recurso pode ser escalado para qualquer serviço" — ou seja, não precisei marcar nada para o Corte Masculino já valer para o João. Esse é exatamente o tipo de default sensato que faltou na tela de Horários.

11. **Cadastrar cliente (`/clientes`).** O modal de novo agendamento na Agenda só permite escolher um cliente já cadastrado — não dá para criar um cliente novo sem sair do modal. Fui em Clientes, cadastrei "Carlos Cliente Teste" com telefone. O telefone foi formatado automaticamente com DDI (`+5511999998888`) — bom detalhe. **P2**: falta atalho para criar cliente direto do modal de agendamento (cenário comum: cliente aparece na loja pedindo horário).
12. **Criar agendamento (`/agenda`).** Cliquei num horário livre no grid do dia. Ao tentar às 15:00 do próprio dia, recebi um erro claro via toast: "Não é possível agendar em um horário que já passou" (fazia sentido — já era tarde da noite no horário local do teste). O modal **não fechou** e os dados preenchidos (cliente, serviço) **não se perderam** — ótimo tratamento de erro, sem me obrigar a preencher tudo de novo. Mudei a data para o dia seguinte, 15:00, e o agendamento foi criado com sucesso (`201 Created`).

### Resumo Persona A

- **Travou?** Sim, duas vezes: (1) e-mail de confirmação nunca chegou — travamento P0 real, contornado só com acesso a banco de dados; (2) horário de trabalho vazio por padrão, quase invisível — travamento silencioso P1, só percebido por atenção redobrada.
- **Precisou de conhecimento técnico que uma pessoa comum não teria?** Sim, para destravar o passo 1 (SQL direto no banco). Sem isso, um dono de barbearia real ficaria parado na tela "Confirme seu e-mail" para sempre.
- **A tela seguinte era previsível?** Na maior parte, sim — os defaults (slug automático, fuso horário, terminologia, "serviço vale para todos os profissionais") são bons. As duas exceções foram justamente os dois pontos de trava acima.
- **Pagaria por isso?** Pelo fluxo em si de cadastro/configuração, sim — é rápido e claro quando funciona. Mas a falha de e-mail, se reproduzir em produção, é motivo de desistência total antes mesmo de começar a usar.

---

## Persona B — Cliente final tentando agendar (sem login)

**Objetivo:** acessar `/{slug}` do estabelecimento que a Persona A criou e marcar um horário do zero, como alguém que nunca viu o site.

### Passo a passo

1. Acessei `http://localhost:3000/barbearia-corte-certo` sem estar logado. A página já mostra, sem exigir nenhuma ação: os serviços oferecidos ("Corte Masculino, 30 min, R$40,00") e a equipe ("João Barbeiro"). O título da aba do navegador já veio com o nome do estabelecimento — bom sinal de branding funcionando.
2. **Passo 1 de 3 — Serviço.** Cliquei no único serviço disponível.
3. **Passo 2 de 3 — Data e horário.** A data padrão era "hoje". Para essa data apareceu: "Nenhum horário disponível nesta data. Tente outra data." com um botão alternativo "Entrar na lista de espera para este dia" — **bom tratamento de caso vazio**, não é um beco sem saída (fazia sentido não ter horário: já era noite, fora do expediente configurado). Troquei a data para o dia seguinte (quarta-feira) e os horários apareceram em grade de 15 em 15 minutos, das 09:00 às 17:30, com os horários já ocupados pelo agendamento da Persona A corretamente bloqueados (o sistema bloqueou 14:45, 15:00 e 15:15 ao redor de um agendamento de 30 min marcado às 15:00 — motor de conflito funcionando certo, inclusive considerando a duração do serviço, não só o horário exato).
4. Selecionei 10:00.
5. **Passo 3 de 3 — Seus dados.** A tela mostrou um resumo claro antes de pedir meus dados: "Corte Masculino · Quarta-feira, 19 de agosto às 10:00" — reduz a ansiedade de "será que cliquei no horário certo?". Preenchi Nome, E-mail e Telefone (todos sem exigir cadastro de senha ou conta — ótimo, cliente final não devia precisar criar conta só para agendar).
6. Cliquei "Confirmar agendamento". Recebi na hora: "Agendamento confirmado! Corte Masculino em Quarta-feira, 19 de agosto às 10:00. Enviamos os detalhes para [e-mail]." Chamada de API confirmada com `201 Created`.

### 🛑 Mesmo problema da Persona A

Verifiquei o MailHog de novo: o e-mail de confirmação do agendamento **também não chegou**, mesmo minutos depois. Isso é mais grave para a Persona B que para a A: um cliente final anônimo **não tem painel, não tem login, não tem nada** — o e-mail de confirmação é o único comprovante que ele teria do agendamento. Se ele quiser conferir depois "marquei mesmo às 10h ou 10h30?", não tem para onde olhar. **P0** (mesma raiz do achado da Persona A, mas o impacto aqui é maior porque não existe bypass possível para esse usuário).

### Resumo Persona B

- **Travou?** No fluxo de agendamento em si, não — completou as 3 etapas sem nenhum erro, sem precisar voltar, sem confusão. Foi o fluxo mais fluido de toda a auditoria.
- **Precisou de conhecimento técnico que uma pessoa comum não teria?** Não. Zero jargão, zero passo desnecessário, não pediu criação de conta.
- **A tela seguinte era previsível?** Sim, o wizard de 3 passos com resumo antes da confirmação final é exatamente o padrão que qualquer pessoa já viu em outros apps de agendamento.
- **Pagaria... digo, agendaria de novo?** Sim, a experiência de agendar é boa o bastante para eu confiar e voltar — **contanto que o e-mail de confirmação realmente chegasse**. Do jeito que está, eu sairia da página sem nenhuma certeza escrita do meu horário.

---

## Persona C — Dona de salão testando o financeiro

**Objetivo:** logada como dono, entender o Financeiro sem ajuda, lançar uma despesa, ver o fluxo de caixa, entender os números.

### Passo a passo

1. Fui em `/financeiro` (logada como Persona A/Ze da Barbearia, já que o objetivo era testar a área, não recriar outro tenant). A página tem 4 abas: **Resumo, Contas a Receber, Contas a Pagar, Comissões** — nomes em português simples, sem jargão contábil.
2. Aba **Resumo** (padrão): mostrava Entradas R$0,00, Saídas R$0,00, Saldo R$0,00, com gráfico "Evolução mensal" e "Despesas por categoria" ambos vazios, com texto explicativo ("Sem dados suficientes para este período") em vez de gráfico quebrado ou tela em branco.
3. Fui em **Contas a Pagar**, cliquei "Nova despesa". Modal simples: Descrição, Valor, Vencimento, Categoria (dropdown com Aluguel, Insumos, Comissão, Salário, Contas, Marketing, Outros — categorias que uma dona de salão reconheceria de cara). Lancei "Aluguel do salão", R$1.200, vencimento 25/08. Salvou e apareceu na lista como "Pendente".

### ⚠️ Ponto de confusão (não travamento)

Voltei para a aba **Resumo** para ver se a despesa recém-lançada aparecia em algum lugar — não aparecia nada. Saídas continuava R$0,00, "Despesas por categoria" continuava dizendo "Nenhuma despesa paga no período". Entendi depois que o Resumo só conta o que já foi **pago**, não o que está pendente — o que é uma decisão de design defensável (fluxo de caixa = realizado), mas na hora, sem nenhuma explicação na tela, dá a impressão de que o lançamento não funcionou. **P2**.

4. Voltei em Contas a Pagar e cliquei "Marcar como pago". Apareceu um diálogo de confirmação: "Marcar 'Aluguel do salão' (R$1.200,00) como pago. Essa ação não pode ser desfeita." — confirmação apropriada para uma ação financeira. Confirmei.
5. Voltei ao Resumo: agora sim, Saídas R$1.200,00, Saldo -R$1.200,00, e o gráfico "Despesas por categoria" mostrando "Aluguel R$1.200,00" corretamente. **Os números batem e atualizam na hora.**

### Resumo Persona C

- **Travou?** Não.
- **Precisou de conhecimento técnico ou contábil que uma pessoa comum não teria?** Não — os rótulos são do dia a dia de um pequeno negócio, não termos de contabilidade.
- **A tela seguinte era previsível?** Quase toda, com uma exceção: o Resumo não deixar claro, sem eu ter que "descobrir", que só soma o que está pago — um aviso do tipo "você tem R$1.200 a pagar pendente" no Resumo teria fechado esse buraco (o mesmo padrão do bloco "Requer sua atenção" que já existe no Painel principal).
- **Pagaria por isso?** Sim — a mecânica financeira, uma vez entendida, é confiável e simples de operar no dia a dia.

---

## Persona D — Profissional autônomo avaliando se vale a pena

**Objetivo:** percorrer o fluxo de escolha de plano/assinatura como quem está decidindo se paga ou não.

### Passo a passo

1. Na **landing page**, a seção "Preços simples, sem surpresa" mostra 3 planos pagos — Essencial (R$49,90, "para quem está começando e trabalha sozinho" — a descrição perfeita para esta persona), Profissional (R$99,90) e Avançado (R$199,90) — sem nenhuma opção gratuita visível ali. Como autônomo, o plano Essencial parecia feito sob medida para mim.
2. Cliquei em "Começar com Essencial" → fui redirecionado para `/onboarding`, que **não tem nenhuma referência a qual plano eu cliquei** — comecei o mesmo formulário genérico de 3 passos de qualquer outro cadastro.
3. Depois de criar a conta, a tela "Escolha seu plano" mostrou só **2 opções**: "Grátis" e "Padrão R$99,00/mês". **Nenhuma delas é o "Essencial R$49,90" que eu tinha acabado de escolher na landing page.** Isso é desorientador: cliquei em um plano específico e cheguei em uma tela com planos diferentes, sem explicação de "por que mudou" ou "o Essencial virou o Padrão?".
4. O card do "Padrão" informa "14 dias grátis, depois cobrado automaticamente. Cancele quando quiser." — essa informação de trial não aparece em lugar nenhum da landing page antes disso, então é a primeira vez que eu, como autônomo decidindo se vale a pena, fico sabendo que existe um período de teste.
5. **Não avancei o pagamento real** (cadastro de cartão via checkout Asaas) — isso está fora do que posso testar: envolve inserir dados de cartão em um checkout de verdade, o que não é uma ação que devo executar. Então **não testei** a etapa final de cobrança/checkout nem o quão claro é o processo de cartão obrigatório mencionado no changelog do projeto (Fase 24).

### Resumo Persona D

- **Travou?** Não tecnicamente — dava para prosseguir escolhendo Grátis ou Padrão. Mas a experiência **quebra a promessa** feita um clique antes, na landing page.
- **Precisou de conhecimento técnico?** Não, mas precisou de tolerância a inconsistência: eu, como autônomo comparando preço, chegaria a essa tela achando que ou o site está com bug, ou fui enganado no preço.
- **A tela seguinte era previsível?** Não — foi a maior surpresa negativa de toda a auditoria: o plano que cliquei simplesmente não existe na tela seguinte.
- **Pagaria por isso?** Com a informação que tenho hoje, eu **hesitaria seriamente**. Não é o produto que me faz duvidar — é essa desconexão entre o que foi anunciado e o que foi entregue no exato momento em que eu estou mais perto de decidir pagar. Numa decisão de compra real, esse é o tipo de detalhe que faz a pessoa fechar a aba.
- **Etapa de checkout/cartão (Fase 24):** não testada, por estar fora do que posso executar (inserção de dados financeiros). Recomendo teste dedicado desse trecho por um humano ou por um ambiente de sandbox de pagamento.

---

## Achados consolidados desta parte (ver também Product Audit para os priorizados com impacto comercial)

| # | Achado | Persona(s) | Severidade |
|---|---|---|---|
| 1 | E-mail de confirmação (cadastro e agendamento) nunca chega | A, B | P0 |
| 2 | Novo profissional sem horário de trabalho por padrão → agenda pública fica vazia | A, B | P1 |
| 3 | Plano escolhido na landing page não corresponde às opções reais no cadastro | D | P0 (comercial) |
| 4 | Cancelar assinatura sem nenhuma confirmação | (Product Audit) | P0 |
| 5 | Resumo financeiro não sinaliza contas pendentes, só pagas | C | P2 |
| 6 | Sem atalho para cadastrar cliente novo dentro do modal de agendamento | A | P2 |
| 7 | Etapa de checkout/cartão (Fase 24) não testada nesta rodada | D | Não testado |
