"use client";

import * as React from "react";
import Link from "next/link";
import {
  ArrowRight,
  Search,
  Users,
  Palette,
  Wallet,
  ShieldCheck,
  Bell,
  CalendarDays,
  CalendarPlus,
  LifeBuoy,
  MessageCircle,
  UserPlus,
  X,
  type LucideIcon,
} from "lucide-react";

import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from "@/components/ui/accordion";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";

type FaqEntry = { question: string; answer: string };
type FaqCategory = { id: string; title: string; icon: LucideIcon; items: FaqEntry[] };
type VisibleFaqEntry = FaqEntry & { categoryId: string; categoryTitle: string; icon: LucideIcon };

const CATEGORY_DESCRIPTIONS: Record<string, string> = {
  "conta-equipe": "Acesso, convites e permissões",
  "marca-unidades": "Portal público e filiais",
  "agenda-clientes": "Horários, clientes e faltas",
  financeiro: "Planos, comissões e pontos",
  notificacoes: "Lembretes e mensagens",
  seguranca: "Conta, MFA e privacidade",
};

const QUICK_ACTIONS: { title: string; description: string; href: string; icon: LucideIcon }[] = [
  {
    title: "Criar agendamento",
    description: "Abra a agenda e reserve um horário.",
    href: "/agenda?novo=1",
    icon: CalendarPlus,
  },
  {
    title: "Convidar equipe",
    description: "Adicione uma pessoa ao estabelecimento.",
    href: "/settings/team",
    icon: UserPlus,
  },
  {
    title: "Personalizar portal",
    description: "Ajuste cores, logo e informações públicas.",
    href: "/settings/branding",
    icon: Palette,
  },
  {
    title: "Configurar WhatsApp",
    description: "Prepare lembretes automáticos.",
    href: "/settings/whatsapp",
    icon: MessageCircle,
  },
];

// Categorias cobrindo as areas do produto que mais geram duvida (equipe,
// marca, agenda/unidades, financeiro, notificacoes, seguranca) -- a versao
// anterior desta pagina tinha so 6 perguntas soltas, sem nenhuma organizacao.
const CATEGORIES: FaqCategory[] = [
  {
    id: "conta-equipe",
    title: "Conta e equipe",
    icon: Users,
    items: [
      {
        question: "Como convido alguém para a minha equipe?",
        answer:
          "Vá em Empresa → Equipe e use o botão de convidar. A pessoa recebe um link por e-mail para criar a senha e já entra com o papel de Equipe (sem acesso a dados da empresa ou do plano).",
      },
      {
        question: "Quais as diferenças entre os papéis (Administrador e Equipe)?",
        answer:
          "Administrador (Owner) vê e edita tudo — marca, plano, dados cadastrais, financeiro. Equipe (Staff) acessa o dia a dia — agenda, clientes, serviços — sem ver dados sensíveis da empresa ou do plano.",
      },
      {
        question: "Quem pode ver os dados cadastrais da empresa (CNPJ, razão social)?",
        answer:
          "Só o administrador da conta. Membros da equipe têm acesso apenas ao que precisam para atender clientes — agenda, clientes, serviços — sem ver dados cadastrais ou informações do plano.",
      },
      {
        question: "Esqueci minha senha, e agora?",
        answer:
          "Na tela de login, use \"Esqueceu a senha?\" para receber um link de redefinição por e-mail. Se preferir mais segurança, ative a verificação em duas etapas em Configurações → Segurança.",
      },
    ],
  },
  {
    id: "marca-unidades",
    title: "Marca e unidades",
    icon: Palette,
    items: [
      {
        question: "Como personalizo as cores, a fonte e o logo do meu portal público?",
        answer:
          "Em Empresa → Marca você edita aparência, conteúdo e informações do seu portal com uma prévia ao vivo. Só o administrador da conta pode salvar alterações.",
      },
      {
        question: "Minha cor de marca foi rejeitada ao salvar — por quê?",
        answer:
          "O sistema exige contraste suficiente entre a cor escolhida e o texto branco dos botões (padrão de acessibilidade AA). Se o aviso aparecer, escolha um tom mais escuro da mesma cor.",
      },
      {
        question: "Tenho mais de uma loja ou filial — preciso cadastrar unidades?",
        answer:
          "Só se fizer sentido para o seu negócio. Em Empresa → Locais de atendimento você cadastra cada endereço e vincula profissionais e agendamentos a ele. Um negócio de endereço único pode usar apenas um local, sem etapas extras para o cliente.",
      },
    ],
  },
  {
    id: "agenda-clientes",
    title: "Agenda e clientes",
    icon: CalendarDays,
    items: [
      {
        question: "Como evito que dois clientes marquem o mesmo horário?",
        answer:
          "Não precisa fazer nada — o motor de agendamento bloqueia horários conflitantes automaticamente por profissional (ou sala, se configurado). Bloqueios, folgas e horário especial também entram nessa checagem.",
      },
      {
        question: "Como funciona a lista de espera?",
        answer:
          "Quando um horário procurado está lotado, o cliente pode entrar na lista de espera em Lista de espera. Se vagar um horário compatível, o sistema avisa automaticamente.",
      },
      {
        question: "Posso cobrar um depósito para reduzir faltas (no-show)?",
        answer:
          "Sim. Configure a política de no-show e o valor do depósito obrigatório nas configurações de agendamento — o cliente paga uma parte antecipada para confirmar o horário.",
      },
    ],
  },
  {
    id: "financeiro",
    title: "Financeiro, estoque e fidelidade",
    icon: Wallet,
    items: [
      {
        question: "Como troco de plano ou cancelo minha assinatura?",
        answer:
          "Em Minha conta → Plano (visível só para o administrador) você vê o plano atual, pode assinar um plano pago ou cancelar. Se você já pagou o período atual, o acesso continua até ele vencer, mesmo cancelando antes — a não ser que ainda esteja no teste grátis.",
      },
      {
        question: "Por que o link \"Ver fatura\" às vezes não aparece?",
        answer:
          "Por segurança, o link só fica disponível a partir de 10 dias antes do vencimento da fatura atual. Fora dessa janela, a data em que ele vai aparecer é mostrada no lugar do link.",
      },
      {
        question: "Como funciona a comissão por profissional?",
        answer:
          "Cadastre o percentual ou valor de comissão de cada profissional em Barbeiros/Equipe. O relatório de comissões em Financeiro calcula automaticamente com base nos atendimentos concluídos.",
      },
      {
        question: "Como funciona o programa de fidelidade?",
        answer:
          "Em Relacionamento → Fidelidade você define quantos pontos cada real gerado vale e o que pode ser resgatado. Os pontos são creditados automaticamente a cada atendimento concluído.",
      },
    ],
  },
  {
    id: "notificacoes",
    title: "Notificações e WhatsApp",
    icon: Bell,
    items: [
      {
        question: "Como conecto o WhatsApp para lembretes automáticos?",
        answer:
          "Em Relacionamento → WhatsApp, conecte sua conta e configure os modelos de mensagem (agendado, lembrete, cancelado, etc.). Sem essa conexão, os lembretes automáticos não são enviados.",
      },
      {
        question: "Posso desligar algum lembrete específico?",
        answer:
          "Sim. Em Configurações → Notificações você liga ou desliga cada gatilho (confirmação, lembrete 24h, lembrete 2h, pós-atendimento) por e-mail e por WhatsApp de forma independente.",
      },
    ],
  },
  {
    id: "seguranca",
    title: "Segurança e privacidade",
    icon: ShieldCheck,
    items: [
      {
        question: "O que é a verificação em duas etapas (MFA)?",
        answer:
          "Uma camada extra de proteção: além da senha, o login pede um código gerado por um aplicativo autenticador (Google Authenticator, Authy...). Ative em Configurações → Segurança.",
      },
      {
        question: "Como sei se alguém mais acessou minha conta?",
        answer:
          "Em Configurações → Segurança, o card \"Atividade recente\" mostra os últimos logins e eventos de segurança da sua conta. Se algo parecer estranho, use \"Sair de todos os dispositivos\" e troque sua senha.",
      },
      {
        question: "Meus dados e os dos meus clientes ficam seguros?",
        answer:
          "Sim. Senhas nunca ficam em texto simples, dado sensível (como CPF) é criptografado, e cada estabelecimento só enxerga os próprios dados — nunca de outro negócio na plataforma.",
      },
    ],
  },
];

const DIACRITICS_PATTERN = new RegExp("[\\u0300-\\u036f]", "g");

function normalize(value: string): string {
  return value.toLowerCase().normalize("NFD").replace(DIACRITICS_PATTERN, "");
}

export default function HelpSettingsPage() {
  const [search, setSearch] = React.useState("");
  const [selectedCategory, setSelectedCategory] = React.useState<string | null>(null);

  const filteredCategories = React.useMemo(() => {
    const term = normalize(search.trim());
    if (!term) return CATEGORIES;

    return CATEGORIES.map((category) => ({
      ...category,
      items: category.items.filter(
        (item) => normalize(item.question).includes(term) || normalize(item.answer).includes(term)
      ),
    })).filter((category) => category.items.length > 0);
  }, [search]);

  const totalResults = filteredCategories.reduce((sum, category) => sum + category.items.length, 0);
  const visibleEntries = React.useMemo<VisibleFaqEntry[]>(() => {
    const hasSearch = search.trim().length > 0;
    const categories = hasSearch
      ? filteredCategories
      : selectedCategory
        ? CATEGORIES.filter((category) => category.id === selectedCategory)
        : CATEGORIES.map((category) => ({ ...category, items: category.items.slice(0, 1) }));

    return categories.flatMap((category) =>
      category.items.map((item) => ({
        ...item,
        categoryId: category.id,
        categoryTitle: category.title,
        icon: category.icon,
      }))
    );
  }, [filteredCategories, search, selectedCategory]);

  const selectedCategoryTitle = CATEGORIES.find((category) => category.id === selectedCategory)?.title;
  const resultsTitle = search.trim()
    ? "Resultados da busca"
    : selectedCategoryTitle ?? "Perguntas mais acessadas";

  function selectCategory(categoryId: string | null) {
    setSelectedCategory(categoryId);
    setSearch("");
  }

  return (
    <div className="flex w-full max-w-5xl flex-1 flex-col gap-8">
      <section className="from-primary/[0.14] via-card to-card relative overflow-hidden rounded-2xl border border-primary/20 bg-gradient-to-br p-5 shadow-sm sm:p-8">
        <div className="bg-primary/10 pointer-events-none absolute -top-16 -right-12 size-48 rounded-full blur-3xl" />
        <div className="relative max-w-2xl">
          <span className="text-primary mb-3 flex items-center gap-2 text-xs font-semibold tracking-wider uppercase">
            <LifeBuoy className="size-4" aria-hidden="true" />
            Central de ajuda
          </span>
          <h2 className="text-2xl font-semibold tracking-tight sm:text-3xl">Como podemos ajudar?</h2>
          <p className="text-muted-foreground mt-2 text-sm sm:text-base">
            Encontre respostas e acesse rapidamente as configurações mais usadas do AgendioBR.
          </p>

          <div className="relative mt-6">
            <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-4 size-5 -translate-y-1/2" aria-hidden="true" />
            <Input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Busque por senha, comissão, WhatsApp..."
              aria-label="Buscar na central de ajuda"
              className="bg-background/90 h-12 rounded-xl pr-11 pl-12 text-base shadow-sm"
            />
            {search && (
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                onClick={() => setSearch("")}
                aria-label="Limpar busca"
                className="absolute top-1/2 right-2 -translate-y-1/2"
              >
                <X className="size-4" />
              </Button>
            )}
          </div>
        </div>
      </section>

      <section aria-labelledby="quick-actions-title">
        <div className="mb-3 flex items-end justify-between gap-3">
          <div>
            <h2 id="quick-actions-title" className="text-base font-semibold">Atalhos rápidos</h2>
            <p className="text-muted-foreground text-sm">Vá direto para as tarefas mais procuradas.</p>
          </div>
        </div>
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          {QUICK_ACTIONS.map((action) => {
            const Icon = action.icon;
            return (
              <Link
                key={action.href}
                href={action.href}
                className="bg-card hover:bg-card-hover group flex min-h-32 flex-col rounded-xl border border-border/80 p-4 shadow-sm transition-all hover:-translate-y-0.5 hover:border-primary/40"
              >
                <div className="bg-primary/12 text-primary mb-4 flex size-9 items-center justify-center rounded-lg">
                  <Icon className="size-4" aria-hidden="true" />
                </div>
                <span className="flex items-center justify-between gap-2 text-sm font-semibold">
                  {action.title}
                  <ArrowRight className="text-muted-foreground size-4 transition-transform group-hover:translate-x-0.5" aria-hidden="true" />
                </span>
                <span className="text-muted-foreground mt-1 text-xs leading-relaxed">{action.description}</span>
              </Link>
            );
          })}
        </div>
      </section>

      {!search.trim() && (
        <section aria-labelledby="topics-title">
          <div className="mb-3">
            <h2 id="topics-title" className="text-base font-semibold">Explore por assunto</h2>
            <p className="text-muted-foreground text-sm">Escolha uma área para ver todas as respostas relacionadas.</p>
          </div>
          <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
            {CATEGORIES.map((category) => {
              const Icon = category.icon;
              const isSelected = selectedCategory === category.id;
              return (
                <button
                  key={category.id}
                  type="button"
                  onClick={() => selectCategory(isSelected ? null : category.id)}
                  aria-pressed={isSelected}
                  className={`flex items-center gap-3 rounded-xl border p-3 text-left transition-colors ${
                    isSelected
                      ? "border-primary/50 bg-primary/10"
                      : "bg-card border-border/80 hover:border-primary/30 hover:bg-card-hover"
                  }`}
                >
                  <span className="bg-primary/12 text-primary flex size-10 shrink-0 items-center justify-center rounded-lg">
                    <Icon className="size-5" aria-hidden="true" />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm font-medium">{category.title}</span>
                    <span className="text-muted-foreground block truncate text-xs">{CATEGORY_DESCRIPTIONS[category.id]}</span>
                  </span>
                  <span className="text-muted-foreground text-xs tabular-nums">{category.items.length}</span>
                </button>
              );
            })}
          </div>
        </section>
      )}

      <section aria-labelledby="faq-title">
        <div className="mb-3 flex flex-wrap items-end justify-between gap-3">
          <div>
            <h2 id="faq-title" className="text-base font-semibold">{resultsTitle}</h2>
            <p className="text-muted-foreground text-sm">
              {search.trim()
                ? `${totalResults} ${totalResults === 1 ? "resposta encontrada" : "respostas encontradas"}.`
                : selectedCategory
                  ? "Selecione uma pergunta para ver a resposta."
                  : "Um ponto de partida para as dúvidas mais comuns."}
            </p>
          </div>
          {selectedCategory && !search.trim() && (
            <Button type="button" variant="ghost" size="sm" onClick={() => selectCategory(null)}>
              Ver assuntos principais
            </Button>
          )}
        </div>

        {visibleEntries.length === 0 ? (
          <Card className="border-border/80 shadow-sm">
            <CardContent>
              <EmptyState
                icon={Search}
                title="Nenhum resultado para essa busca"
                description="Tente uma palavra mais curta ou procure pelo nome de uma funcionalidade."
                action={<Button variant="outline" size="sm" onClick={() => setSearch("")}>Limpar busca</Button>}
              />
            </CardContent>
          </Card>
        ) : (
          <Card className="border-border/80 shadow-sm">
            <CardContent className="pt-2">
              <Accordion type="multiple" className="divide-y divide-border/70">
                {visibleEntries.map(({ question, answer, categoryTitle, icon: Icon }) => (
                  <AccordionItem key={question} value={question} className="border-0">
                    <AccordionTrigger className="py-4 text-left hover:no-underline">
                      <span className="flex min-w-0 items-start gap-3">
                        <span className="bg-primary/10 text-primary mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-lg">
                          <Icon className="size-4" aria-hidden="true" />
                        </span>
                        <span>
                          <span className="block font-medium">{question}</span>
                          <span className="text-muted-foreground mt-0.5 block text-xs font-normal">{categoryTitle}</span>
                        </span>
                      </span>
                    </AccordionTrigger>
                    <AccordionContent className="text-muted-foreground pr-4 pb-5 pl-11 text-sm leading-relaxed">
                      {answer}
                    </AccordionContent>
                  </AccordionItem>
                ))}
              </Accordion>
            </CardContent>
          </Card>
        )}
      </section>

      <Card className="from-primary/[0.09] to-card overflow-hidden border-primary/20 bg-gradient-to-r shadow-sm">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <MessageCircle className="text-primary size-5" aria-hidden="true" />
            Ainda precisa de ajuda?
          </CardTitle>
          <CardDescription>Conte o que aconteceu e envie sua dúvida diretamente para nossa equipe.</CardDescription>
        </CardHeader>
        <CardContent>
          <Button asChild>
            <Link href="/settings/feedback">
              Enviar uma mensagem
              <ArrowRight className="size-4" aria-hidden="true" />
            </Link>
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
