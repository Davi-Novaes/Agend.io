"use client";

import * as React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { SessionProvider } from "@/lib/auth/session-context";
import { PlatformSessionProvider } from "@/lib/auth/platform-session-context";
import { TenantThemeProvider } from "@/lib/tenant/tenant-theme-provider";
import { Toaster } from "@/components/ui/sonner";

export function Providers({ children }: { children: React.ReactNode }) {
  const [queryClient] = React.useState(() => new QueryClient());

  return (
    <QueryClientProvider client={queryClient}>
      <TenantThemeProvider>
        <SessionProvider>
          <PlatformSessionProvider>
            {children}
            <Toaster />
          </PlatformSessionProvider>
        </SessionProvider>
      </TenantThemeProvider>
    </QueryClientProvider>
  );
}
