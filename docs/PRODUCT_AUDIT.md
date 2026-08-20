# Auditoria de Produto — Agendio

**Papel:** Product Owner avaliando se um dono de estabelecimento real pagaria por isso.
**Data:** 2026-08-18. **Ambiente:** frontend `localhost:3000` + backend `localhost:5071`, navegador real (Browser pane), Postgres/MailHog/RabbitMQ locais via Docker.
**Como foi avaliado:** navegação real ponta a ponta (ver `docs/UX_AUDIT.md` para o passo a passo das 4 personas). Nenhuma funcionalidade foi inventada — tudo aqui reflete o que existe hoje em `main`.

---

## 1. Proposta de valor — fica clara em menos de 2 minutos?

**Sim, na landing page.** O hero ("O sistema de agendamento que se adapta ao seu negócio"), a lista de 12 segmentos, a seção "Tudo que o seu negócio precisa" e o FAQ ("Preciso saber programar...?") comunicam bem, em português claro, para quem é o produto e por que ele é diferente de uma agenda genérica. A ideia de vocabulário adaptado por segmento ("Cliente" vira "Paciente"/"Tutor") é um diferencial real e bem comunicado.

**Não, na página de preços vs. o que o cadastro realmente oferece** — ver achado P0 abaixo. Isso quebra a clareza assim que o usuário sai da landing page e entra no fluxo real.

## 2. Onboarding — um leigo consegue cadastrar sem travar?

Quase. O formulário de 3 passos (estabelecimento → segmento → conta) é objetivo, com bons defaults (slug gerado automaticamente do nome do negócio, fuso horário detectado do navegador, prévia de terminologia do segmento). Isso está alinhado com o princípio norteador do projeto.

Dois problemas reais:
- A tela "Escolha seu plano" pós-cadastro mostra **2 planos** (Grátis e Padrão R$99/mês) que **não batem** com os 3 planos anunciados na landing page (Essencial R$49,90 / Profissional R$99,90 / Avançado R$199,90). Ver achado P0.
- O e-mail de confirmação, obrigatório para logar (Fase 23), **não chegou** em nenhuma das duas vezes testadas (cadastro + reenvio), verificado tanto na UI do MailHog quanto na API do MailHog e no banco (`identity.users.email_confirmed_at` permaneceu `NULL`). O evento de outbox foi processado em segundos (`identity.outbox_messages`), então o problema está entre "evento processado" e "e-mail efetivamente entregue" — não é falta de trigger, é falha silenciosa no envio. Só consegui prosseguir os testes fazendo um `UPDATE` direto no banco, algo que um usuário real não pode fazer. Ver achado P0.

## 3. Planos/assinatura — está claro e transparente?

**Não.** Além da inconsistência de preços entre landing page e cadastro (achado P0), o cancelamento de assinatura em `/settings/billing` acontece **sem nenhuma confirmação** — um único clique em "Cancelar assinatura" já dispara `POST /api/billing/subscription/cancel` e muda o status para "Cancelada" instantaneamente. Não há modal de "tem certeza?", não há aviso sobre o que acontece com os dados, não há oferta de retenção. Compare com a ação "Marcar como pago" no Financeiro, que É protegida por um diálogo de confirmação ("Essa ação não pode ser desfeita") — ou seja, o time já sabe fazer confirmação de ação destrutiva, só não aplicou aqui, no lugar de maior risco comercial (perda de assinatura paga).

Depois de cancelar, a tela de "Assinar Grátis" pede Nome completo, CPF ou CNPJ e mostra o texto "R$ 0.00/mês — pague com PIX, boleto ou cartão" — que não faz sentido para um plano gratuito e parece um copy/paste do fluxo pago sem ajuste.

## 4. Dashboard e uso diário — o dono entende o que fazer primeiro?

**Sim.** Esse foi o ponto mais forte do teste. O Painel redesenhado tem estados vazios bem escritos com CTA direto ("Você não possui agendamentos para hoje. Que tal criar um novo agendamento?"), KPIs simples (Faturamento, Despesas, Resultado) e um bloco "Requer sua atenção" que centraliza pendências. Um dono sem experiência em software entende a tela sem precisar de tutorial.

## 5. Cancelamento — existe caminho claro?

Existe e é fácil de achar (`Settings → Plano`, um botão visível). O problema não é encontrabilidade, é **segurança**: é fácil demais, ao ponto de ser perigoso (ver item 3). "Claro" sim; "seguro", não.

---

## Achados priorizados e sugestões

### P0 — Preços da landing page não existem no produto real
- **Problema:** a landing page anuncia 3 planos pagos (Essencial R$49,90, Profissional R$99,90, Avançado R$199,90, sem opção gratuita visível), mas `GET /api/billing/plans` só retorna 2 planos (Grátis R$0 e Padrão R$99,00) e é isso que aparece na tela real de escolha de plano no cadastro. Um cliente que decide assinar com base na landing page encontra um produto diferente do anunciado.
- **Público afetado:** todo lead que vem pela landing page — especialmente Persona D (autônomo comparando preço antes de decidir).
- **Benefício da correção:** elimina risco de percepção de propaganda enganosa e reconstrói a confiança na primeira interação real com o produto.
- **Prioridade:** P0.
- **Impacto comercial:** alto — é o tipo de discrepância que gera cancelamento no primeiro dia e review negativo ("anunciam um preço e cobram outro").
- **Complexidade:** baixa a média — ou os 3 planos passam a existir de fato no backend de billing, ou a landing page é atualizada para refletir os 2 planos reais. É decisão de produto, não só de código.

### P0 — E-mail de confirmação (e de agendamento) não chega
- **Problema:** nenhum e-mail transacional testado chegou ao MailHog local — nem o de confirmação de cadastro, nem o de confirmação de agendamento da página pública — apesar do evento de domínio ser processado em segundos no outbox. Como login exige e-mail confirmado (Fase 23), isso trava o dono de estabelecimento fora do próprio painel sem intervenção manual no banco.
- **Público afetado:** todo novo cadastro (Persona A) e todo cliente final que agenda pela página pública sem estar logado (Persona B), que depende do e-mail como único comprovante.
- **Benefício da correção:** destrava o onboarding e dá ao cliente final um comprovante de agendamento confiável.
- **Prioridade:** P0.
- **Impacto comercial:** altíssimo — sem isso, ninguém consegue ativar conta pelo fluxo normal em produção se o mesmo problema existir lá.
- **Complexidade:** desconhecida sem investigação de código/infra — pode ser configuração de ambiente local (fora do escopo desta auditoria) ou falha real de envio; recomendo que o time confirme se isso reproduz fora deste ambiente de teste antes de tratar como bug de produção.

### P0 — Cancelamento de assinatura sem confirmação
- **Problema:** `Cancelar assinatura` executa a ação imediatamente, sem modal de confirmação, aviso de consequência ou possibilidade de desfazer — ao contrário de outras ações destrutivas do mesmo produto (ex.: "Marcar como pago" no Financeiro).
- **Público afetado:** todo dono de estabelecimento com assinatura ativa, principalmente em plano pago.
- **Benefício da correção:** evita perda acidental de assinatura e a fricção de suporte que isso gera.
- **Prioridade:** P0.
- **Impacto comercial:** alto — cancelamento acidental em plano pago é receita perdida e ticket de suporte irritado.
- **Complexidade:** baixa — é o mesmo padrão de `AlertDialog` já usado no Financeiro, só precisa ser aplicado aqui.

### P1 — Recurso novo não tem horário de trabalho por padrão
- **Problema:** ao cadastrar um profissional/recurso, nenhum horário de atendimento vem preenchido. Sem configurar manualmente em "Horários" (uma tela separada, um clique a mais que não é óbvio a partir do cadastro), a página pública de agendamento mostra "Nenhum horário disponível" para qualquer data. Isso contraria diretamente o princípio do projeto ("funciona com defaults sensatos, sem o dono do negócio configurar nada?").
- **Público afetado:** todo novo estabelecimento (Persona A) e, por consequência, todo cliente final tentando agendar (Persona B) antes que o dono descubra esse passo extra.
- **Benefício:** o negócio fica "agendável" imediatamente após o cadastro do primeiro profissional, sem etapa escondida.
- **Prioridade:** P1.
- **Impacto comercial:** médio-alto — é exatamente o tipo de "trava silenciosa" que faz um usuário leigo desistir sem nem entender por quê.
- **Complexidade:** baixa — pré-preencher um horário comercial padrão (ex.: seg-sáb 09:00-18:00) editável, em vez de vazio.

### P1 — Sessão pode cair sozinha durante navegação normal
- **Problema:** durante o teste, chamadas duplicadas a `POST /api/auth/refresh` ocorreram repetidamente em navegação normal (troca de página), e em um momento o refresh retornou 401 e a sessão caiu, exigindo login novamente. O CLAUDE.md documenta que reuso de refresh token revoga a família inteira — chamadas de refresh concorrentes com o mesmo token são candidatas a disparar exatamente esse mecanismo de segurança contra o próprio usuário legítimo.
- **Público afetado:** qualquer usuário logado usando o painel por período prolongado.
- **Benefício:** elimina logouts inesperados que corroem a confiança no produto no dia a dia.
- **Prioridade:** P1.
- **Impacto comercial:** médio — não impede o uso, mas gera frustração recorrente ("por que fui deslogado do nada?").
- **Complexidade:** média — requer investigação do interceptor de refresh no frontend (possível disparo duplicado por múltiplos consumidores da mesma chamada).

### P2 — Financeiro não avisa sobre contas pendentes no resumo
- **Problema:** ao lançar uma despesa como "Pendente", o painel "Resumo" do Financeiro continua mostrando R$ 0,00 em tudo — nada indica que existe uma conta a pagar aguardando. Só depois de marcar como paga é que ela aparece nos números.
- **Público afetado:** donos usando o Financeiro para decisão (Persona C).
- **Benefício:** reduz a sensação de "cadê minha despesa?" e ajuda a planejar caixa futuro, não só o realizado.
- **Prioridade:** P2.
- **Impacto comercial:** médio — Financeiro é um diferencial de venda do produto; qualquer confusão ali reduz a percepção de confiabilidade dos números.
- **Complexidade:** baixa — reaproveitar o padrão já existente do bloco "Requer sua atenção" do Painel.

### P2 — Reassinar o plano Grátis pede CPF/CNPJ e fala em pagamento
- **Problema:** após cancelar, o formulário de reassinatura do plano Grátis pede "CPF ou CNPJ" e exibe "R$ 0.00/mês — pague com PIX, boleto ou cartão", mensagens que não fazem sentido para um plano sem cobrança.
- **Público afetado:** qualquer usuário revisitando/reassinando o plano gratuito.
- **Benefício:** remove fricção e confusão desnecessárias num plano que deveria ser instantâneo.
- **Prioridade:** P2.
- **Impacto comercial:** baixo-médio — mais sobre percepção de qualidade/acabamento do que bloqueio de receita.
- **Complexidade:** baixa — texto e campos condicionais por tipo de plano.

### P2 — Terminologia por segmento não chega até a página "Recursos"
- **Problema:** o onboarding promete que "Profissional" vira o termo do segmento (ex. "Barbeiro"), mas a página de gestão continua rotulada genericamente como "Recursos" mesmo para uma barbearia.
- **Público afetado:** todos os segmentos, principal diferencial de produto citado na landing page.
- **Benefício:** cumpre a promessa feita na landing page/onboarding, reforçando o principal diferencial de posicionamento.
- **Prioridade:** P2.
- **Impacto comercial:** médio — é justamente o argumento de venda ("o sistema se adapta a você") que fica incompleto.
- **Complexidade:** baixa-média — a terminologia já existe (usada no onboarding); é questão de propagar para os rótulos de menu/página.

### P2 — Não dá para cadastrar cliente novo direto do modal de agendamento
- **Problema:** ao criar um agendamento pela Agenda, só é possível selecionar um cliente já existente; para um cliente novo é preciso sair do modal, ir em Clientes, cadastrar, e voltar.
- **Público afetado:** dono atendendo um walk-in/ligação, cenário comum no dia a dia de barbearia/salão.
- **Benefício:** agiliza o caso mais comum do dia a dia (cliente novo pedindo horário na hora).
- **Prioridade:** P2.
- **Impacto comercial:** médio — fricção operacional recorrente, não é bloqueio único.
- **Complexidade:** média — adicionar opção "+ novo cliente" inline no combobox do modal.

---

## Veredito do Product Owner

O núcleo do produto é sólido: onboarding com bons defaults, dashboard claro, financeiro com números corretos, motor de agendamento com prevenção de conflito bem implementada, página pública de agendamento fácil de usar mesmo sem login. O produto entende o problema do dono de estabelecimento pequeno e fala a língua dele.

Mas os dois achados P0 comerciais (preços que não batem e cancelamento sem confirmação) são o tipo de coisa que decide se um cliente real confia o cartão dele a esse sistema. Combinados com a falha de e-mail (que bloqueou o fluxo de teste por completo até eu intervir direto no banco), esse conjunto de problemas — não a qualidade do produto em si — é o que hoje me faria hesitar antes de recomendar assinatura paga a um dono de negócio real. São problemas concentrados e corrigíveis, não uma reformulação de produto.
