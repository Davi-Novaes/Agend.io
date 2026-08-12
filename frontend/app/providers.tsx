"use client";

import * as React from "react";
import { ThemeProvider } from "next-themes";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { SessionProvider } from "@/lib/auth/session-context";
import { PlatformSessionProvider } from "@/lib/auth/platform-session-context";
import { Toaster } from "@/components/ui/sonner";
import { TooltipProvider } from "@/components/ui/tooltip";

// TenantThemeProvider NAO entra aqui — envolveria toda a arvore (painel do
// dono, login, admin) com a paleta padrao de tenant em vez do design system
// global. Ele fica local em app/(public)/[slug]/page.tsx, o unico lugar que
// realmente precisa de branding por tenant.
export function Providers({ children }: { children: React.ReactNode }) {
  const [queryClient] = React.useState(() => new QueryClient());

  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider attribute="class" defaultTheme="system" enableSystem>
        <TooltipProvider>
          <SessionProvider>
            <PlatformSessionProvider>
              {children}
              <Toaster />
            </PlatformSessionProvider>
          </SessionProvider>
        </TooltipProvider>
      </ThemeProvider>
    </QueryClientProvider>
  );
}
