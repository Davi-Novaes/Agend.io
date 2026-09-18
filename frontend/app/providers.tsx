"use client";

import * as React from "react";
import { ThemeProvider } from "next-themes";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { SessionProvider } from "@/lib/auth/session-context";
import { PlatformSessionProvider } from "@/lib/auth/platform-session-context";
import { Toaster } from "@/components/ui/sonner";
import { TooltipProvider } from "@/components/ui/tooltip";

// TenantThemeProvider NAO entra aqui — envolveria toda a arvore (painel do
// dono, admin) com uma paleta fixa em vez do design system global. Cada
// pagina que precisa de uma cor de destaque propria (app/(public)/[slug],
// app/(auth)/login) aplica localmente, sem afetar o resto do app.
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
