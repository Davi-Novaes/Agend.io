"use client";

import * as React from "react";
import Link from "next/link";
import { Menu, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { MarketingThemeToggle } from "@/components/marketing/marketing-theme-toggle";

const LINKS = [
  { href: "#showcase", label: "Produto" },
  { href: "#funcionalidades", label: "Recursos" },
  { href: "#como-funciona", label: "Como funciona" },
  { href: "#precos", label: "Planos" },
];

export function MobileNav() {
  const [open, setOpen] = React.useState(false);

  return (
    <div className="sm:hidden">
      <Button
        type="button"
        variant="ghost"
        size="icon"
        aria-expanded={open}
        aria-controls="mobile-nav-menu"
        aria-label={open ? "Fechar menu" : "Abrir menu"}
        onClick={() => setOpen((v) => !v)}
      >
        {open ? <X className="size-5" /> : <Menu className="size-5" />}
      </Button>

      {open && (
        <div
          id="mobile-nav-menu"
          className="bg-background absolute inset-x-0 top-full border-b p-4 shadow-lg"
        >
          <nav className="flex flex-col gap-3">
            {LINKS.map((link) => (
              <a
                key={link.href}
                href={link.href}
                onClick={() => setOpen(false)}
                className="text-foreground py-1.5 text-sm font-medium"
              >
                {link.label}
              </a>
            ))}
            <div className="mt-2 flex items-center justify-between gap-2 border-t pt-3">
              <span className="text-muted-foreground text-sm">Tema</span>
              <MarketingThemeToggle />
            </div>
            <div className="flex flex-col gap-2">
              <Button variant="outline" asChild>
                <Link href="/login">Entrar</Link>
              </Button>
              <Button asChild>
                <Link href="/onboarding">Cadastre-se</Link>
              </Button>
            </div>
          </nav>
        </div>
      )}
    </div>
  );
}
