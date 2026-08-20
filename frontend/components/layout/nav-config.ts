import type { LucideIcon } from "lucide-react";
import {
  LayoutDashboard,
  CalendarDays,
  Users,
  Sparkles,
  Armchair,
  Wallet,
  Package,
  BarChart3,
  Megaphone,
  Building2,
  Palette,
  UserCog,
  CreditCard,
  ShieldCheck,
  MessageCircle,
  Bell,
  Gift,
  Hourglass,
  Banknote,
  Bot,
} from "lucide-react";

export type NavItem = {
  href: string;
  label: string;
  icon: LucideIcon;
};

export type NavGroup = {
  label: string;
  items: NavItem[];
};

// Fonte unica de navegacao — consumida pela Sidebar (grupos) e pelo Header
// (usePathname() busca o item com href batendo, pra exibir o titulo da pagina).
export const NAV_GROUPS: NavGroup[] = [
  {
    label: "Visão Geral",
    items: [
      { href: "/painel", label: "Painel", icon: LayoutDashboard },
      { href: "/assistente", label: "Assistente", icon: Bot },
    ],
  },
  {
    label: "Gestão",
    items: [
      { href: "/agenda", label: "Agenda", icon: CalendarDays },
      { href: "/waitlist", label: "Lista de espera", icon: Hourglass },
      { href: "/clientes", label: "Clientes", icon: Users },
      { href: "/servicos", label: "Serviços", icon: Sparkles },
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
      { href: "/settings/units", label: "Unidades", icon: Building2 },
      { href: "/settings/branding", label: "Marca", icon: Palette },
      { href: "/settings/team", label: "Equipe", icon: UserCog },
      { href: "/settings/billing", label: "Plano", icon: CreditCard },
    ],
  },
  {
    label: "Configurações",
    items: [{ href: "/settings/security", label: "Segurança", icon: ShieldCheck }],
  },
];

export const NAV_ITEMS: NavItem[] = NAV_GROUPS.flatMap((group) => group.items);

// O onboarding promete que "Profissional" vira o termo do segmento (ex.
// "Barbeiro" numa barbearia), mas so o item de Recursos precisava mudar aqui
// — os demais rotulos do menu (Clientes, Servicos, Agenda) nao fazem parte
// deste achado (BL-14, docs/BACKLOG.md), entao ficam com o rotulo estatico
// de sempre em vez de virar uma revisao geral de vocabulario nao pedida.
export function resolveNavLabel(href: string, defaultLabel: string, staffPlural?: string): string {
  return href === "/recursos" && staffPlural ? staffPlural : defaultLabel;
}
