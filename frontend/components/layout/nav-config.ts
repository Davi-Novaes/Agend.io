import type { LucideIcon } from "lucide-react";
import {
  LayoutDashboard,
  CalendarDays,
  Users,
  Tag,
  Armchair,
  Wallet,
  Package,
  BarChart3,
  Megaphone,
  Building2,
  Palette,
  UserCog,
  ShieldCheck,
  MessageCircle,
  Bell,
  Gift,
  Hourglass,
  Banknote,
  UserRound,
  HelpCircle,
  MessageSquareHeart,
} from "lucide-react";

export type NavItem = {
  href: string;
  label: string;
  icon: LucideIcon;
  /** So visivel/acessivel para quem tem papel Owner (ex.: Plano/Assinatura, Equipe, Empresa). */
  ownerOnly?: boolean;
  /** O item fica "ativo" (destaque na Sidebar) para qualquer sub-rota, nao so o href exato — usado por Marca, que tem sub-navegacao interna (ver app/(app)/settings/branding/layout.tsx). */
  matchPrefix?: boolean;
};

export type NavGroup = {
  label: string;
  items: NavItem[];
};

// Fonte unica de navegacao — consumida pela Sidebar (grupos) e pelo Header
// (usePathname() busca o item com href batendo, pra exibir o titulo da pagina).
//
// Cada modulo e um item proprio na Sidebar (restaurado — uma tentativa
// anterior de colapsar tudo num hub generico "Configuracoes da conta" deixou
// a navegacao dificil de usar). "Configuracoes" agora e so o que realmente
// e sobre a CONTA do usuario (Seguranca, Minha conta); Unidades/Marca/Equipe/
// Plano viram o grupo "Empresa"; WhatsApp/Notificacoes/Fidelidade viram
// "Relacionamento"; Pagamentos vira parte de "Financeiro" — cada assunto no
// grupo a que pertence, em vez de tudo debaixo de uma unica sombrinha
// "Configuracoes" so porque tecnicamente sao todos formularios de ajuste.
export const NAV_GROUPS: NavGroup[] = [
  {
    label: "Visão Geral",
    items: [{ href: "/painel", label: "Painel", icon: LayoutDashboard }],
  },
  {
    label: "Gestão",
    items: [
      { href: "/agenda", label: "Agenda", icon: CalendarDays },
      { href: "/waitlist", label: "Lista de espera", icon: Hourglass },
      { href: "/clientes", label: "Clientes", icon: Users },
      { href: "/servicos", label: "Serviços", icon: Tag },
      { href: "/recursos", label: "Recursos", icon: Armchair },
    ],
  },
  {
    label: "Financeiro",
    items: [
      { href: "/financeiro", label: "Financeiro", icon: Wallet },
      { href: "/estoque", label: "Estoque", icon: Package },
      { href: "/settings/payments", label: "Pagamentos", icon: Banknote },
    ],
  },
  {
    label: "Análises",
    items: [{ href: "/relatorios", label: "Relatórios", icon: BarChart3 }],
  },
  {
    label: "Relacionamento",
    items: [
      { href: "/marketing", label: "Marketing", icon: Megaphone },
      { href: "/settings/whatsapp", label: "WhatsApp", icon: MessageCircle },
      { href: "/settings/notifications", label: "Notificações", icon: Bell },
      { href: "/settings/loyalty", label: "Fidelidade", icon: Gift },
    ],
  },
  {
    label: "Empresa",
    items: [
      // Unidades: back-end restringe o grupo /api/units inteiro a Owner
      // (nem a leitura fica aberta) -- ownerOnly aqui evita Staff clicar e
      // cair num 403. Marca/Equipe seguem o padrao oposto: leitura liberada
      // a qualquer papel (o backend so bloqueia ESCRITA a Owner), entao
      // ficam visiveis com o formulario desabilitado pra quem nao e Owner
      // (ver fieldset em cada pagina). Dados da empresa e Plano NAO estao
      // mais aqui -- moveram pra dentro de Minha conta (Configuracoes,
      // abaixo), pedido explicito do usuario (2026-09-05): membro comum nao
      // deve ver nem essas duas abas nem os dados delas (backend tambem
      // passou a rejeitar a leitura com 403 pra quem nao e Owner).
      { href: "/settings/units", label: "Locais de atendimento", icon: Building2, ownerOnly: true },
      { href: "/settings/branding", label: "Marca", icon: Palette, matchPrefix: true },
      { href: "/settings/team", label: "Equipe", icon: UserCog },
    ],
  },
  {
    label: "Configurações",
    items: [
      { href: "/settings/security", label: "Segurança", icon: ShieldCheck },
      // matchPrefix: Empresa/Plano (Owner) vivem em /settings/account/company
      // e /settings/account/plan, sub-abas do mesmo mini-builder de Marca.
      { href: "/settings/account", label: "Minha conta", icon: UserRound, matchPrefix: true },
      { href: "/settings/help", label: "Ajuda e informações", icon: HelpCircle },
      { href: "/settings/feedback", label: "Feedback", icon: MessageSquareHeart },
    ],
  },
];

export const NAV_ITEMS: NavItem[] = NAV_GROUPS.flatMap((group) => group.items);

// Titulo do AppHeader (usePathname() casa href exato OU, com matchPrefix,
// qualquer sub-rota — ver Marca acima) — nao ha mais uma lista separada de
// "itens de configuracoes", NAV_ITEMS ja cobre tudo.
export const ALL_NAV_ITEMS: NavItem[] = NAV_ITEMS;

// O onboarding promete que "Profissional" vira o termo do segmento (ex.
// "Barbeiro" numa barbearia), mas so o item de Recursos precisava mudar aqui
// — os demais rotulos do menu (Clientes, Servicos, Agenda) nao fazem parte
// deste achado (BL-14, docs/BACKLOG.md), entao ficam com o rotulo estatico
// de sempre em vez de virar uma revisao geral de vocabulario nao pedida.
export function resolveNavLabel(href: string, defaultLabel: string, staffPlural?: string): string {
  return href === "/recursos" && staffPlural ? staffPlural : defaultLabel;
}
