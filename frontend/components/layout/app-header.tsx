"use client";

import { usePathname, useRouter } from "next/navigation";
import { LogOut } from "lucide-react";

import { NAV_ITEMS } from "@/components/layout/nav-config";
import { useSession } from "@/lib/auth/session-context";
import { decodeJwtEmail, decodeJwtFullName } from "@/lib/auth/decode-jwt";
import { ThemeToggle } from "@/components/theme-toggle";
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

  const title = NAV_ITEMS.find((item) => item.href === pathname)?.label ?? "Agendio";
  const email = session ? decodeJwtEmail(session.accessToken) : null;
  const fullName = session ? decodeJwtFullName(session.accessToken) : null;

  function handleLogout() {
    logout();
    router.replace("/login");
  }

  return (
    <header className="flex h-14 shrink-0 items-center gap-2 border-b px-4">
      <SidebarTrigger />
      <Separator orientation="vertical" className="h-4" />
      <h1 className="text-sm font-medium">{title}</h1>
      <div className="ml-auto flex items-center gap-1">
        <ThemeToggle />
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button className="ml-1" aria-label="Menu da conta">
              <Avatar className="size-7">
                {/* bg-muted/text-muted-foreground nao atinge contraste AA em text-xs
                    (achado pelo axe-core no e2e) — usa o par primary/primary-foreground,
                    ja auditado para AA (ver components/ui/button.tsx). */}
                <AvatarFallback className="bg-primary text-xs text-primary-foreground">
                  {initialsFrom(fullName, email)}
                </AvatarFallback>
              </Avatar>
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
