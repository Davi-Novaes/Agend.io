"use client";

import Link from "next/link";
import { CheckCircle2, Circle } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { cn } from "@/lib/utils";

type ChecklistItem = { label: string; description: string; href: string; done: boolean };

/**
 * So aparece enquanto faltar servico ou profissional cadastrado -- nesse
 * ponto o negocio literalmente nao consegue receber um agendamento ainda,
 * entao substitui a parede de cards vazios (cada widget do painel com seu
 * proprio "sem dados") por um unico roteiro do que falta. Some sozinho
 * assim que os 3 passos forem concluidos.
 */
export function OnboardingChecklistCard({
  servicesCount,
  resourcesCount,
  hasAppointments,
}: {
  servicesCount: number;
  resourcesCount: number;
  hasAppointments: boolean;
}) {
  const items: ChecklistItem[] = [
    {
      label: "Cadastre seu primeiro servico",
      description: "O que voce oferece e quanto custa.",
      href: "/servicos",
      done: servicesCount > 0,
    },
    {
      label: "Adicione um profissional",
      description: "Quem atende -- mesmo que seja so voce.",
      href: "/recursos",
      done: resourcesCount > 0,
    },
    {
      label: "Receba seu primeiro agendamento",
      description: "Compartilhe sua pagina publica ou crie um agendamento manual.",
      href: "/agenda?novo=1",
      done: hasAppointments,
    },
  ];

  const doneCount = items.filter((item) => item.done).length;
  if (doneCount === items.length) {
    return null;
  }

  return (
    <Card className="ring-primary/30 bg-primary/5">
      <CardContent>
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <div>
            <h3 className="text-sm font-semibold">Primeiros passos</h3>
            <p className="text-muted-foreground text-xs">Complete o basico para comecar a receber clientes.</p>
          </div>
          <Badge variant={doneCount === 0 ? "outline" : "success"}>
            {doneCount} de {items.length} concluidos
          </Badge>
        </div>
        <ul className="flex flex-col gap-2">
          {items.map((item) => (
            <li key={item.label}>
              <Link
                href={item.href}
                className={cn(
                  "flex items-start gap-3 rounded-lg border p-3 transition-colors",
                  item.done ? "border-transparent" : "hover:border-primary/40 hover:bg-accent/50"
                )}
              >
                {item.done ? (
                  <CheckCircle2 className="text-success mt-0.5 size-5 shrink-0" aria-hidden="true" />
                ) : (
                  <Circle className="text-muted-foreground mt-0.5 size-5 shrink-0" aria-hidden="true" />
                )}
                <div className="min-w-0 flex-1">
                  <p className={cn("text-sm font-medium", item.done && "text-muted-foreground line-through")}>{item.label}</p>
                  {!item.done && <p className="text-muted-foreground text-xs">{item.description}</p>}
                </div>
              </Link>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
