"use client";

import { usePathname, useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { Bell, ChevronDown, LogOut } from "lucide-react";

import { NAV_ITEMS, resolveNavLabel } from "@/components/layout/nav-config";
import { getTenantProfile } from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { decodeJwtEmail, decodeJwtFullName, decodeJwtRole } from "@/lib/auth/decode-jwt";
import { ThemeToggle } from "@/components/theme-toggle";
import { Button } from "@/components/ui/button";
import { SidebarTrigger } from "@/components/ui/sidebar";
import { Separator } from "@/components/ui/separator";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

const ROLE_LABELS: Record<"Owner" | "Staff", string> = {
  Owner: "Administrador",
  Staff: "Equipe",
};

function initialsFrom(fullName: string | null, email: string | null): string {
  if (fullName) {
    const words = fullName.trim().split(/\s+/);
    const firstInitial = words[0]?.[0] ?? "";
    const lastInitial = words.length > 1 ? words[words.length - 1][0] : words[0]?.[1] ?? "";
    return `${firstInitial}${lastInitial}`.toUpperCase();
  }
  if (email) {
    return email.slice(0, 2).toUpperCase();
  }
  return "?";
}

export function AppHeader() {
  const pathname = usePathname();
  const router = useRouter();
  const { session, logout } = useSession();
  // Mesma fonte de terminologia do AppSidebar — evita o titulo do header
  // dizer "Recursos" enquanto o menu ja diz "Barbeiros" (BL-14, docs/BACKLOG.md).
  const profileQuery = useQuery({
    queryKey: ["tenant-profile"],
    queryFn: () => getTenantProfile(session!.accessToken),
    enabled: Boolean(session),
  });

  const matchedItem = NAV_ITEMS.find((item) => item.href === pathname);
  const title = matchedItem
    ? resolveNavLabel(matchedItem.href, matchedItem.label, profileQuery.data?.terminology.staffPlural)
    : "Agendio";
  const email = session ? decodeJwtEmail(session.accessToken) : null;
  const fullName = session ? decodeJwtFullName(session.accessToken) : null;
  const role = session ? decodeJwtRole(session.accessToken) : null;

  function handleLogout() {
    logout();
    router.replace("/login");
  }

  return (
    <header className="flex h-14 shrink-0 items-center gap-2 border-b px-4">
      <SidebarTrigger />
      <Separator orientation="vertical" className="h-4" />
      <h1 className="text-sm font-medium">{title}</h1>
      <div className="ml-auto flex items-center gap-2">
        <ThemeToggle />
        {/* Sem contador: nao ha um sistema de notificacoes de verdade ainda —
            mostrar um numero aqui seria inventar dado. Fica so o icone,
            estrutura pronta pra quando existir uma inbox real. */}
        <Button variant="ghost" size="icon" className="text-muted-foreground" aria-label="Notificacoes">
          <Bell className="size-4.5" />
        </Button>
        <Separator orientation="vertical" className="h-6" />
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button className="flex items-center gap-2 rounded-md py-1 pl-1 pr-2 hover:bg-accent" aria-label="Menu da conta">
              <Avatar className="size-8">
                {/* bg-muted/text-muted-foreground nao atinge contraste AA em text-xs
                    (achado pelo axe-core no e2e) — usa o par primary/primary-foreground,
                    ja auditado para AA (ver components/ui/button.tsx). */}
                <AvatarFallback className="bg-primary text-xs text-primary-foreground">
                  {initialsFrom(fullName, email)}
                </AvatarFallback>
              </Avatar>
              <span className="hidden flex-col items-start leading-tight sm:flex">
                <span className="max-w-40 truncate text-sm font-medium">{fullName ?? email ?? "Minha conta"}</span>
                {role && <span className="text-xs text-muted-foreground">{ROLE_LABELS[role]}</span>}
              </span>
              <ChevronDown className="text-muted-foreground hidden size-4 sm:block" aria-hidden="true" />
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuLabel className="truncate font-normal">
              {fullName ?? email ?? "Minha conta"}
              {fullName && email && <p className="truncate text-xs text-muted-foreground">{email}</p>}
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={handleLogout}>
              <LogOut className="size-4" />
              Sair
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
