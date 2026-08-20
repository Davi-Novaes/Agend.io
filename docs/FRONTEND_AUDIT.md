# Auditoria de Frontend — Agendio

**Escopo:** todo o app autenticado exceto Dashboard/`painel` (já redesenhado nesta sessão e fora
de escopo), páginas de auth (`/login`, `/onboarding`), páginas públicas (`/[slug]`,
`/[slug]/avaliar`, `/confirm-email/[token]`, `/invitations/[token]`) e painel administrativo da
plataforma (`/admin/*`).

**Método:** leitura completa de todas as páginas em `frontend/app/**/page.tsx` (26 arquivos, ~11 mil
linhas) e dos componentes de layout/compartilhados (`components/layout`, `components/ui`,
`components/shared`, `components/public/booking-flow.tsx`). Adicionalmente, navegação real no
navegador (via `npm run dev` já rodando em `localhost:3000`) nas páginas `/login` e `/onboarding`,
com redimensionamento para 375×812 (mobile) e inspeção de texto/estrutura renderizada
(`get_page_text`/`read_page`) para confirmar que classes responsivas (`hidden lg:flex` etc.)
realmente escondem/mostram conteúdo no navegador, não só no código.

**Limitação a declarar:** a captura de screenshot não funcionou neste ambiente (o painel do
navegador não estava "compositando" frames), então a verificação visual pixel-a-pixel não foi
possível. Toda a análise de responsividade/visual das páginas autenticadas (agenda, clientes,
financeiro, etc.) foi feita por **leitura de código** (classes Tailwind `sm:`/`lg:`, estrutura de
grid, largura de diálogos) — não por navegação real, já que essas rotas exigem login e não havia
credencial de teste disponível. Isso está sinalizado em cada achado como "leitura de código".
Também não foi lido linha a linha o conteúdo completo de alguns componentes de apoio grandes
(`customer-profile-sheet.tsx`, `cash-flow-chart.tsx`, trechos finais de `estoque/page.tsx` —
aba Movimentações — e `financeiro/page.tsx` — aba Comissões, e o restante de `branding/page.tsx`
após a seção de perfil/contato); foram verificados por amostragem e grep, não width-a-width.

---

## Resumo do que já está bem estabelecido (baseline positivo)

Antes dos achados: a maior parte do app segue um padrão consistente e bom, que vale generalizar
em vez de reinventar:

- **Estados de lista**: praticamente todas as páginas de CRUD (`clientes`, `servicos`, `recursos`,
  `waitlist`, `marketing`, `financeiro`, `estoque`, `settings/units`, `settings/notifications`)
  usam `Skeleton` para loading de tabela, `EmptyState` (ícone + título + descrição + ação) para
  lista vazia, e `toast.error(...)` em todo `onError` de mutation — verificado mutation a mutation,
  não só por contagem.
- **Formulários**: `react-hook-form` + `zodResolver` + componente `Form`/`FormField` do shadcn em
  praticamente 100% dos formulários, com `FormLabel` associado via `htmlFor` automaticamente —
  boa base de acessibilidade "de graça".
- **Confirmação de ações destrutivas/financeiras**: existe um padrão bom com `AlertDialog`
  (`financeiro` → confirmar recebimento, `marketing` → revisar e confirmar envio de campanha,
  `admin/subscriptions` → cancelar assinatura de um tenant), mas ele **não foi aplicado em todo
  lugar** — ver achados P1 #1 e #2 abaixo.
- **Diálogos** (`components/ui/dialog.tsx`) já são responsivos por padrão
  (`max-w-[calc(100%-2rem)] sm:max-w-sm`, com override pontual para `sm:max-w-md`), então nenhum
  modal encontrado deveria estourar a tela em mobile.
- **Contraste AA**: `settings/branding` valida a cor primária/secundária contra `#FFFFFF` com
  `meetsAaContrast`/`contrastRatio` (`lib/tenant/contrast.ts`) antes de salvar, e a página pública
  reaproveita esse mesmo cálculo — atende a exigência do CLAUDE.md.
- **Página pública `/[slug]`**: SSR (bom para SEO), seções com `aria-labelledby`, grids responsivos
  (`sm:grid-cols-2 lg:grid-cols-3`), fluxo de agendamento (`BookingFlow`) com `aria-live` no
  indicador de passo, `role="alert"` nos erros de formulário e fallback de lista de espera quando
  não há horário — é a página mais bem cuidada de todo o escopo auditado.

---

## Achados

| ID | Página/Área | Descrição | Severidade | Arquivo |
|----|-------------|-----------|------------|---------|
| F01 | Configurações → Plano | "Cancelar assinatura" dispara `cancelMutation.mutate()` direto no `onClick`, sem nenhuma confirmação — cancela uma assinatura paga em produção com um único clique acidental possível. Inconsistente com o padrão de `AlertDialog` já usado em Financeiro, Marketing e no próprio painel Admin para ações equivalentes. | **P1** | `frontend/app/(app)/settings/billing/page.tsx` (~L159-167) |
| F02 | Admin → Estabelecimentos | Botão "Desativar"/"Ativar" muda o status de um tenant inteiro (bloqueia o acesso de um cliente pagante ao sistema) direto no `onClick`, sem confirmação. É a ação com maior "raio de explosão" de todo o app sem nenhuma barreira, e está inconsistente com a página irmã `admin/subscriptions`, que usa `AlertDialog` para uma ação de risco comparável. | **P1** | `frontend/app/(platform)/admin/tenants/page.tsx` (~L92-101) |
| F03 | Agenda | Reagendar por arrastar-e-soltar usa a API nativa de drag-and-drop HTML5 (`onDragStart`/`onDragOver`/`onDrop`), que **não tem equivalente em touch** — não funciona em tablet/celular, comum em recepção de salão/clínica. Não bloqueia o fluxo (o botão "Remarcar" dentro do dialog de detalhe é 100% acessível por clique/teclado/touch), mas o atalho "rápido" simplesmente não faz nada em touch, sem nenhum aviso. | **P1** | `frontend/app/(app)/agenda/page.tsx` (~L397-414, L487-496) |
| F04 | Configurações → Marca | Dois grupos de campo usam `grid grid-cols-2 gap-4` **sem** prefixo `sm:` (Telefone/WhatsApp e Instagram/Facebook), diferente do resto do formulário (que é 1 coluna) e do padrão do resto do app (`sm:grid-cols-2`). Em 375px de largura, dois inputs de ~150px cada para textos como URLs fica apertado. Leitura de código, não confirmado no navegador. | P2 | `frontend/app/(app)/settings/branding/page.tsx` (L811, L865) |
| F05 | Página pública — Agendamento | `BookingFlow` não trata erro de rede/API nas 3 queries que alimentam os passos (serviços, recursos, horários) — só existe tratamento para `isLoading` e para lista vazia (`length === 0`). Se a API falhar temporariamente, a tela mostra exatamente a mesma coisa que "este estabelecimento não tem serviços cadastrados", o que pode custar agendamentos reais sem que ninguém perceba que é um erro técnico. | P2 | `frontend/components/public/booking-flow.tsx` (~L189-192, queries L75-90) |
| F06 | Lista de espera | Botão "Cancelar" de uma entrada da fila dispara `cancelMutation.mutate(entry.id)` direto no `onClick`, sem `AlertDialog` — mesmo padrão de risco de F01/F02, em escala menor. | P2 | `frontend/app/(app)/waitlist/page.tsx` (~L210-212) |
| F07 | Agenda | Grade de dia/semana tem colunas com largura mínima fixa (`minmax(9rem, 1fr)`) dentro de um `overflow-x-auto` sem nenhuma affordance visual (sombra, indicador, scrollbar customizada) indicando que dá para rolar horizontalmente. Com 2+ recursos ativos em tela de celular, o usuário pode não perceber que há mais colunas fora da tela. Leitura de código. | P2 | `frontend/app/(app)/agenda/page.tsx` (~L424-434) |
| F08 | Padrão sistêmico — todas as páginas com `<Table>` | Todas as tabelas (Serviços, Clientes, Recursos, Lista de espera, Financeiro, Estoque, Marketing, Unidades, Notificações) dependem só do `overflow-x-auto` embutido no componente `Table` para mobile — funcional, mas em telas de ~375px com 6-7 colunas (ex.: Serviços, Lista de espera) o usuário precisa rolar para o lado para ver Status/Ações, sem nenhuma dica visual de que há mais conteúdo. Não é bug de uma página isolada — é o padrão herdado do componente base `components/ui/table.tsx`; vale avaliar prioridade de coluna ou layout em cartão para telas pequenas antes de ter uso real em campo. | P2 | `frontend/components/ui/table.tsx` + páginas listadas |
| F09 | Admin (Dashboard, Estabelecimentos, Assinaturas) | Estados de loading e erro são texto solto (`"Carregando..."`, `"Não foi possível carregar..."`) em vez de `Skeleton`/`EmptyState`, usados consistentemente no resto do produto. Painel interno (menor exposição a cliente final), mas ainda assim destoa visivelmente do padrão já estabelecido — inclusive do próprio Dashboard redesenhado, que reaproveita `MetricCard` aqui mas não o restante do vocabulário visual. | P2 | `frontend/app/(platform)/admin/page.tsx`, `admin/tenants/page.tsx`, `admin/subscriptions/page.tsx` |
| F10 | Agenda | Chips de agendamento no grid usam texto `text-[11px]` e podem ter altura menor que 24px para agendamentos curtos (a altura é calculada por duração: `Math.max(..., ROW_HEIGHT_PX/2)` = 16px mínimo). Abaixo do alvo mínimo de toque recomendado pelo WCAG 2.2 (critério 2.5.8, AA), relevante para uso em tablet/touch na recepção. Leitura de código. | P2 | `frontend/app/(app)/agenda/page.tsx` (~L58-66, L484) |
| F11 | Padrão de componente — Selects | `settings/team` (campo "Papel" do convite) e `onboarding` (campo "Fuso horário") usam `<select>` HTML nativo em vez do componente `Select` (Radix) usado em todo o resto do app — estilo de foco/borda visivelmente diferente ao navegar por teclado (`focus-visible:ring-3` do `Select` vs. estilo nativo do browser). | P3 | `frontend/app/(app)/settings/team/page.tsx` (~L168-174), `frontend/app/(auth)/onboarding/page.tsx` (~L422-431) |
| F12 | Padrão de acessibilidade — paginação | O texto "Página X de Y" tem `aria-live="polite"` em Clientes, Lista de espera e Histórico de notificações, mas **não** em Serviços, Marketing, Financeiro (Contas a Receber/Pagar) e Recursos, que usam a mesma paginação. Leitor de tela não anuncia a troca de página de forma consistente entre páginas equivalentes. | P3 | `frontend/app/(app)/servicos/page.tsx`, `marketing/page.tsx`, `financeiro/page.tsx`, `recursos/page.tsx` |
| F13 | Admin — layout | O painel administrativo não tem `ThemeToggle` (claro/escuro) nem sidebar — usa um cabeçalho simples com botões de navegação (`AdminNav`), estruturalmente diferente do resto do app (sidebar colapsável + header). Aceitável por ser uma superfície separada (Super Admin ≠ tenant), mas é uma segunda linguagem visual dentro do mesmo produto. | P3 | `frontend/components/platform/admin-nav.tsx`, `frontend/app/(platform)/admin/*` |
| F14 | Configurações → WhatsApp | Campo de token de acesso é `<Input type="password">` sem `autoComplete="off"`/`autoComplete="new-password"` explícito — o navegador pode oferecer para salvar como senha de login no gerenciador de senhas, o que é semanticamente errado (é um token de API de terceiro, não uma credencial da conta Agendio). | P3 | `frontend/app/(app)/settings/whatsapp/page.tsx` (~L199-204) |
| F15 | Página pública — Avaliação | `/[slug]/avaliar` sem o parâmetro `appointmentId` na URL cai direto em `notFound()` (404 genérico do Next, sem a marca do estabelecimento) em vez de uma tela amigável. Caso de borda raro (o link sempre vem com o parâmetro via e-mail/WhatsApp), mas sem tratamento dedicado. | P3 | `frontend/app/(public)/[slug]/avaliar/page.tsx` (~L40) |
| F16 | Agenda — padrão de confirmação | Cancelamento de agendamento usa um mini-formulário inline (motivo + botão "Confirmar cancelamento") dentro do próprio dialog de detalhe, enquanto o resto do app usa o componente `AlertDialog` para o mesmo tipo de confirmação (financeiro, marketing, admin). Funciona e é seguro (exige um segundo clique), mas é uma segunda solução de UI para o mesmo problema — vale considerar consolidar em `AlertDialog` quando mexer nessa tela de novo. | P3 | `frontend/app/(app)/agenda/page.tsx` (~L869-899) |

---

## Detalhe por página/área

### Agenda (`/agenda`)
Grid de dia/semana/mês implementado à mão (sem lib de calendário), com `Skeleton`/`EmptyState`
para recursos sem cadastro, e um dialog de detalhe bem completo (status, histórico de mudanças,
sinal pago via Asaas). Pontos fracos concentrados em touch/mobile: drag-and-drop sem fallback
(F03), colunas com scroll sem affordance (F07), chips pequenos (F10). Formulários de criar/
remarcar/cancelar usam `react-hook-form` + toast — consistentes com o resto do app.

### Clientes (`/clientes`)
Um dos exemplos mais completos do padrão bom: busca com debounce manual (Enter ou botão), filtro
por segmento, importação CSV com relatório de erros via `toast.error`, `EmptyState` diferenciado
para "sem resultado de busca" vs. "nenhum cliente cadastrado", paginação com `aria-live`. Não
foram encontrados problemas de severidade relevante nesta página. `CustomerProfileSheet` e
`CustomerRecoveryCard` (usados aqui) não foram lidos linha a linha — apenas por amostragem.

### Serviços (`/servicos`)
Mesmo padrão de Clientes. Único ponto: paginação sem `aria-live` (F12) e a tabela tem 7 colunas —
mais suscetível ao scroll horizontal em mobile (F08). Upload de imagem tem validação de tipo/
tamanho no cliente com mensagem clara via `toast.error`.

### Recursos (`/recursos`)
Página mais complexa do grupo (988 linhas): cadastro, horário de trabalho por dia da semana,
especialidades, vínculo com serviços, folgas — cada um em seu próprio dialog. Todos seguem o
mesmo padrão de mutation + `toast`. Não foi encontrado problema de severidade alta; vale nota de
que é a página com mais dialogs empilhados (edição abre, e a partir dela mais 3 sub-fluxos), o que
pode confundir em telas pequenas — não confirmado no navegador.

### Lista de espera (`/waitlist`)
Boa explicação do fluxo no texto de topo. Achado relevante: cancelamento sem confirmação (F06).

### Financeiro (`/financeiro`)
Estrutura em abas (Resumo/Receber/Pagar/Comissões) bem organizada, com `PeriodFilter`
reaproveitado do Dashboard. É a única página (fora do Admin) que já usa `AlertDialog` para
confirmar uma ação — "Confirmar recebimento" — o que a torna a referência de padrão para F01/F02/
F06. Aba Comissões não foi lida por completo.

### Estoque (`/estoque`)
Estrutura em abas (Produtos/Movimentações) similar a Financeiro, com filtro de "somente estoque
baixo" e `EmptyState` diferenciado por filtro ativo. Aba Movimentações não foi lida por completo;
por amostragem de grep não há indício de padrão diferente do resto do app.

### Marketing (`/marketing`)
Fluxo de campanha em 2 passos (formulário → `AlertDialog` de revisão com resumo de destinatário/
canal antes de enviar) é o melhor exemplo de "ação irreversível com confirmação" do app inteiro —
deveria ser o modelo para F01/F02/F06.

### Relatórios (`/relatorios`)
Página só de leitura (sem mutations), bem estruturada em seções (`Financeiro`, `Comissões`,
`Agenda`, `Avaliações`, `Estoque`), todas com `Skeleton` e grids responsivos (`sm:grid-cols-2
lg:grid-cols-4`). Nenhum achado relevante.

### Assistente (`/assistente`)
Chat simples e funcional: `EmptyState` com perguntas de exemplo clicáveis, mensagem do usuário
removida da lista se a chamada falhar (evita duplicar pergunta ao tentar de novo), scroll
automático para a última mensagem. Nenhum achado relevante.

### Configurações (`/settings/*`)
Nove páginas, quase todas seguindo o padrão "Skeleton enquanto carrega perfil do tenant → Card
com Form → Switch/Input → toast no submit". Achados específicos: Billing sem confirmação de
cancelamento (F01, o mais sério do grupo), Branding com grid não-responsivo em dois pontos (F04),
Team com `<select>` nativo (F11), WhatsApp com campo de token tipado como senha sem
`autoComplete` (F14). Security (MFA) é a página mais elaborada do grupo (setup com QR code via
lib `qrcode`, códigos de recuperação, desabilitar com senha+código) e não tem achados.

### Login (`/login`) e Onboarding (`/onboarding`)
**Únicas páginas verificadas ao vivo no navegador** (sem exigir sessão autenticada). Confirmado
em 375×812: o painel decorativo à esquerda (`hidden lg:flex`) realmente some do texto renderizado
em mobile — o layout de duas colunas colapsa corretamente para uma coluna centralizada. Fluxo de
login trata 3 casos de erro de forma diferenciada (tenant não encontrado, e-mail não confirmado
com CTA de reenvio, MFA obrigatório) em vez de um toast genérico — boa UX de erro. Onboarding é um
wizard de 3 passos com indicador de progresso (`aria-hidden` na barra + texto "Passo X de Y"
visível, que é o equivalente acessível correto) e uma tela de seleção de plano depois do cadastro
(Fase 24). Achado: `<select>` nativo para fuso horário (F11).

### Página pública `/[slug]`
Ver resumo do "baseline positivo" acima — é o destaque do escopo auditado. Estado vazio quando o
estabelecimento não configurou nada: seções de Serviços/Equipe/Horário simplesmente não renderizam
se vazias (`services.length > 0` etc.), então um tenant novo mostra hero + agendamento + consulta
de fidelidade — não é "feio", mas fica bem enxuto. Dentro do `BookingFlow`, se não houver serviço
cadastrado aparece a mensagem "Nenhum serviço disponível no momento." (tratado). Achado real: erro
de API nas queries não é tratado (F05).

### `/[slug]/avaliar`, `/confirm-email/[token]`, `/invitations/[token]`
Páginas curtas e diretas, com estados de sucesso/erro claros e ícones (`CheckCircle2`/`XCircle`).
Achado pontual em `avaliar` (F15).

### Admin da plataforma (`/admin/*`)
Dashboard reaproveita `MetricCard` do Dashboard novo (bom sinal de que o componente generaliza),
mas o resto da página (loading/erro em texto puro, sem `Skeleton`) e as páginas de Estabelecimentos
e Assinaturas não seguem o mesmo nível de acabamento do resto do produto (F09). Achado mais sério
do grupo é a falta de confirmação ao desativar um tenant (F02) — inconsistente até dentro do
próprio painel, já que Assinaturas usa `AlertDialog` corretamente.

---

## Sobre duplicação de lógica entre páginas (sem propor refatoração)

- O trio busca+paginação+`EmptyState` diferenciado por filtro está copiado quase idêntico em
  Clientes, Serviços, Recursos, Lista de espera, Financeiro e Estoque — já dá pra extrair um hook
  (`usePaginatedQuery`) ou um componente `<PaginatedTable>` sem grande risco, seria só consolidar o
  que já existe.
- `toNullable(value: string)` (trim + `"" → null`) está redefinida em pelo menos 6 arquivos
  (`clientes`, `servicos`, `recursos`, `estoque`, `settings/branding`, `settings/whatsapp`) —
  candidata óbvia para `lib/utils.ts`.
- O padrão de upload de imagem com preview + validação de tipo/tamanho está duplicado entre
  `servicos` (imagem do serviço), `recursos` (foto do recurso) e `settings/branding` (logo/banner),
  cada um com suas próprias constantes `ALLOWED_*_TYPES`/`MAX_*_SIZE_BYTES` repetidas com os mesmos
  valores (2MB, PNG/JPEG/WEBP) — candidato a um hook `useImageUpload`.
