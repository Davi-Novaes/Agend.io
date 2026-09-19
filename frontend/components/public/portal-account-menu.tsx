"use client";

import * as React from "react";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { LogOut, User } from "lucide-react";

import { getCustomerPortal, logoutCustomerPortal } from "@/lib/api/client";
import { Button } from "@/components/ui/button";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

function initialsFrom(fullName: string): string {
  const words = fullName.trim().split(/\s+/);
  const first = words[0]?.[0] ?? "";
  const last = words.length > 1 ? words[words.length - 1][0] : "";
  return `${first}${last}`.toUpperCase();
}

export function PortalAccountMenu({ tenantId, slug, className }: { tenantId: string; slug: string; className: string }) {
  const queryClient = useQueryClient();

  const profileQuery = useQuery({
    queryKey: ["customer-portal", tenantId],
    queryFn: () => getCustomerPortal(tenantId),
    retry: false,
  });

  const logoutMutation = useMutation({
    mutationFn: () => logoutCustomerPortal(tenantId),
    // setQueryData(key, undefined) e um no-op no TanStack Query (updater que
    // resolve pra undefined e ignorado) — removeQueries e o jeito certo de
    // limpar o cache pra a UI voltar pro estado deslogado na hora.
    onSuccess: () => queryClient.removeQueries({ queryKey: ["customer-portal", tenantId] }),
  });

  if (!profileQuery.data) {
    return (
      <Button variant="ghost" size="sm" className={className} asChild>
        <Link href={`/${slug}/minha-conta`}><User className="size-4" /><span className="hidden sm:inline">Minha conta</span></Link>
      </Button>
    );
  }

  const { fullName, email } = profileQuery.data;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button className={`flex items-center gap-2 rounded-md py-1 pl-1 pr-2 hover:bg-accent ${className}`} aria-label="Menu da conta">
          <Avatar className="size-7">
            <AvatarFallback className="bg-primary text-[11px] text-primary-foreground">{initialsFrom(fullName)}</AvatarFallback>
          </Avatar>
          <span className="hidden max-w-32 truncate text-sm font-medium sm:inline">{fullName}</span>
        </button>
      </DropdownMenuTrigger>
      {/* min-w sobrescreve a largura herdada do trigger (DropdownMenuContent
          usa w-(--radix-dropdown-menu-trigger-width) por padrao) — sem isso,
          o menu fica tao estreito quanto o botao avatar+nome e "Meus
          agendamentos" quebra a palavra ao meio. */}
      <DropdownMenuContent align="end" className="min-w-56">
        <DropdownMenuLabel className="truncate font-normal">
          {fullName}
          <p className="truncate text-xs text-muted-foreground">{email}</p>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link href={`/${slug}/minha-conta`} className="whitespace-nowrap"><User className="size-4" />Meus agendamentos</Link>
        </DropdownMenuItem>
        <DropdownMenuItem onClick={() => logoutMutation.mutate()} disabled={logoutMutation.isPending}>
          <LogOut className="size-4" />Sair
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
