"use client";

import * as React from "react";
import { useRouter } from "next/navigation";

import { useSession } from "@/lib/auth/session-context";
import { AppSidebar } from "@/components/layout/app-sidebar";
import { AppHeader } from "@/components/layout/app-header";
import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar";

export default function AppLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const { session, isRestoringSession } = useSession();

  React.useEffect(() => {
    if (!isRestoringSession && !session) {
      router.replace("/login");
    }
  }, [session, isRestoringSession, router]);

  // Enquanto isRestoringSession, ainda nao sabemos se ha um refresh token
  // valido no cookie — redirecionar aqui mandaria pro login uma sessao que
  // so nao terminou de ser restaurada ainda (ex.: logo apos um F5).
  if (isRestoringSession || !session) {
    return null;
  }

  return (
    <SidebarProvider>
      <AppSidebar />
      <SidebarInset>
        <AppHeader />
        <div className="flex-1 p-6 sm:p-10">{children}</div>
      </SidebarInset>
    </SidebarProvider>
  );
}
