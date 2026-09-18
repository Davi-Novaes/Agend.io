"use client";

import * as React from "react";
import Link from "next/link";

import { Logo } from "@/components/logo";
import { MobileNav } from "@/components/marketing/mobile-nav";
import { Button } from "@/components/ui/button";
import { MarketingThemeToggle } from "@/components/marketing/marketing-theme-toggle";
import { NAV_LINKS } from "@/lib/marketing/nav-links";
import { cn } from "@/lib/utils";

/** Transparente sobre o gradiente do hero, ganha fundo solido + blur apos rolar -- so troca de classe, sem re-render de layout. */
export function SiteHeader() {
  const [scrolled, setScrolled] = React.useState(false);

  React.useEffect(() => {
    function onScroll() {
      setScrolled(window.scrollY > 40);
    }
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  return (
    <header
      className={cn(
        "sticky top-0 z-20 border-b transition-colors duration-300",
        scrolled ? "bg-background/80 border-border backdrop-blur" : "border-transparent bg-transparent"
      )}
    >
      <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-4 sm:px-6">
        <Logo />

        <nav className="hidden items-center gap-6 sm:flex">
          {NAV_LINKS.map((link) => (
            <a key={link.href} href={link.href} className="text-muted-foreground hover:text-foreground text-sm transition-colors">
              {link.label}
            </a>
          ))}
        </nav>

        <div className="hidden items-center gap-2 sm:flex">
          <MarketingThemeToggle />
          <Button variant="ghost" asChild>
            <Link href="/login">Entrar</Link>
          </Button>
          <Button asChild className="shadow-sm">
            <Link href="/onboarding">Cadastre-se</Link>
          </Button>
        </div>

        <MobileNav />
      </div>
    </header>
  );
}
