"use client";

import * as React from "react";
import Link from "next/link";
import {
  Search,
  Users,
  Palette,
  Wallet,
  ShieldCheck,
  Bell,
  CalendarDays,
  MessageCircle,
  type LucideIcon,
} from "lucide-react";

import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from "@/components/ui/accordion";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";

type FaqEntry = { question: string; answer: string };
type FaqCategory = { id: string; title: string; icon: LucideIcon; items: FaqEntry[] };

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
          "Só se fizer sentido para o seu negócio. Em Empresa → Unidades você cadastra endereço, cidade, estado e país de cada loja e vincula profissionais e agendamentos a elas. Um negócio de endereço único não precisa configurar nada aqui.",
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

  return (
    <div className="flex w-full max-w-3xl flex-1 flex-col gap-6">
      <div>
        <h1 className="text-lg font-semibold">Ajuda e informações</h1>
        <p className="text-muted-foreground text-sm">
          Respostas rápidas para as dúvidas mais comuns sobre o painel, organizadas por assunto.
        </p>
      </div>

      <div className="relative max-w-md">
        <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2" />
        <Input
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder="Buscar por palavra-chave (ex.: senha, fatura, WhatsApp)"
          className="pl-9"
        />
      </div>

      {totalResults === 0 ? (
        <Card>
          <CardContent>
            <EmptyState icon={Search} title="Nenhum resultado para essa busca." description="Tente outra palavra-chave ou veja todas as perguntas abaixo." />
          </CardContent>
        </Card>
      ) : (
        <div className="flex flex-col gap-4">
          {filteredCategories.map((category) => {
            const Icon = category.icon;
            return (
              <Card key={category.id}>
                <CardHeader>
                  <CardTitle className="flex items-center gap-2 text-base">
                    <Icon className="text-primary size-4" />
                    {category.title}
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <Accordion type="multiple" className="grid gap-3">
                    {category.items.map(({ question, answer }) => (
                      <AccordionItem key={question} value={question} className="border-border rounded-lg border px-4">
                        <AccordionTrigger>{question}</AccordionTrigger>
                        <AccordionContent className="text-muted-foreground">{answer}</AccordionContent>
                      </AccordionItem>
                    ))}
                  </Accordion>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <MessageCircle className="text-primary size-4" />
            Não encontrou o que precisava?
          </CardTitle>
          <CardDescription>Envie sua dúvida ou sugestão diretamente para a nossa equipe.</CardDescription>
        </CardHeader>
        <CardContent>
          <Button asChild variant="outline">
            <Link href="/settings/feedback">Enviar feedback</Link>
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
