"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Bot } from "lucide-react";

import { Logo } from "@/components/logo";
import { NAV_GROUPS } from "@/components/layout/nav-config";
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
} from "@/components/ui/sidebar";

// Assistente tem entrada propria de destaque no rodape (como um atalho de
// IA, nao um item de navegacao comum) — por isso fica de fora do loop de
// grupos abaixo e nao aparece duplicado. Continua em nav-config.ts (unica
// fonte pro href/icone/label, e pro titulo da pagina no AppHeader).
const ASSISTANT_HREF = "/assistente";

export function AppSidebar() {
  const pathname = usePathname();

  return (
    <Sidebar collapsible="icon">
      <SidebarHeader>
        <div className="flex items-center px-2 py-1.5 group-data-[collapsible=icon]:justify-center">
          <Logo className="group-data-[collapsible=icon]:[&>span:last-child]:hidden" />
        </div>
      </SidebarHeader>
      <SidebarContent>
        {NAV_GROUPS.map((group) => {
          const items = group.items.filter((item) => item.href !== ASSISTANT_HREF);
          if (items.length === 0) {
            return null;
          }
          return (
            <SidebarGroup key={group.label}>
              <SidebarGroupLabel>{group.label}</SidebarGroupLabel>
              <SidebarGroupContent>
                <SidebarMenu>
                  {items.map((item) => (
                    <SidebarMenuItem key={item.href}>
                      <SidebarMenuButton
                        asChild
                        isActive={pathname === item.href}
                        tooltip={item.label}
                        className="data-active:bg-primary data-active:text-primary-foreground data-active:hover:bg-primary data-active:hover:text-primary-foreground"
                      >
                        <Link href={item.href}>
                          <item.icon />
                          <span>{item.label}</span>
                        </Link>
                      </SidebarMenuButton>
                    </SidebarMenuItem>
                  ))}
                </SidebarMenu>
              </SidebarGroupContent>
            </SidebarGroup>
          );
        })}
      </SidebarContent>
      <SidebarFooter>
        <Link
          href={ASSISTANT_HREF}
          className="bg-primary text-primary-foreground hover:bg-primary/90 flex items-center gap-2.5 rounded-lg p-2.5 transition-colors group-data-[collapsible=icon]:justify-center group-data-[collapsible=icon]:p-2"
        >
          <Bot className="size-5 shrink-0" aria-hidden="true" />
          <span className="group-data-[collapsible=icon]:hidden">
            <span className="block text-sm font-medium">Assistente IA</span>
            <span className="block text-xs opacity-80">Seu aliado na gestao do negocio</span>
          </span>
        </Link>
      </SidebarFooter>
    </Sidebar>
  );
}
