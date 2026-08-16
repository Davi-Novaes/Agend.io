"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";

import { usePlatformSession } from "@/lib/auth/platform-session-context";
import { Button } from "@/components/ui/button";

const NAV_ITEMS = [
  { href: "/admin", label: "Dashboard" },
  { href: "/admin/tenants", label: "Estabelecimentos" },
  { href: "/admin/subscriptions", label: "Assinaturas" },
];

export function AdminNav() {
  const pathname = usePathname();
  const router = useRouter();
  const { session, logout } = usePlatformSession();

  return (
    <header className="mb-6 flex flex-wrap items-center justify-between gap-4 border-b pb-4">
      <nav className="flex gap-1">
        {NAV_ITEMS.map((item) => (
          <Button key={item.href} variant={pathname === item.href ? "secondary" : "ghost"} size="sm" asChild>
            <Link href={item.href}>{item.label}</Link>
          </Button>
        ))}
      </nav>
      <div className="flex items-center gap-3">
        <span className="text-muted-foreground text-sm">Logado como {session?.fullName}</span>
        <Button
          variant="outline"
          size="sm"
          onClick={() => {
            logout();
            router.replace("/admin/login");
          }}
        >
          Sair
        </Button>
      </div>
    </header>
  );
}
