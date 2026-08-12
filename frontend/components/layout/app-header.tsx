"use client";

import { usePathname, useRouter } from "next/navigation";
import { LogOut } from "lucide-react";

import { NAV_ITEMS } from "@/components/layout/nav-config";
import { useSession } from "@/lib/auth/session-context";
import { decodeJwtEmail } from "@/lib/auth/decode-jwt";
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

function initialsFrom(email: string | null): string {
  if (!email) {
    return "?";
  }
  return email.slice(0, 2).toUpperCase();
}

export function AppHeader() {
  const pathname = usePathname();
  const router = useRouter();
  const { session, logout } = useSession();

  const title = NAV_ITEMS.find((item) => item.href === pathname)?.label ?? "Agendio";
  const email = session ? decodeJwtEmail(session.accessToken) : null;

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
                  {initialsFrom(email)}
                </AvatarFallback>
              </Avatar>
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuLabel className="truncate font-normal text-muted-foreground">
              {email ?? "Minha conta"}
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
