"use client";

import * as React from "react";
import { Bot, X } from "lucide-react";

import { AssistantChat } from "@/components/assistant/assistant-chat";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";

// Pedido explicito do usuario (2026-09-06): nao navegar pra uma pagina cheia
// -- o Assistente IA abre como um popup de chat ancorado no proprio botao
// flutuante, igual widget de chat de site (Intercom/Crisp etc.), disponivel
// em qualquer tela do painel sem tirar a pessoa de onde estava.
export function AssistantWidget() {
  const [open, setOpen] = React.useState(false);

  return (
    <div className="fixed right-6 bottom-6 z-50">
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <button
            type="button"
            aria-label={open ? "Fechar o Assistente IA" : "Abrir o Assistente IA"}
            className="bg-primary text-primary-foreground hover:bg-primary/90 flex size-12 items-center justify-center rounded-full shadow-lg transition-transform hover:scale-105"
          >
            {open ? <X className="size-6" aria-hidden /> : <Bot className="size-6" aria-hidden />}
          </button>
        </PopoverTrigger>
        <PopoverContent
          side="top"
          align="end"
          sideOffset={12}
          className="flex h-[min(600px,70vh)] w-[min(380px,calc(100vw-3rem))] flex-col gap-0 overflow-hidden p-0"
        >
          <div className="border-border flex items-center gap-2 border-b px-4 py-3">
            <span className="bg-primary/10 text-primary flex size-8 shrink-0 items-center justify-center rounded-full">
              <Bot className="size-4" aria-hidden />
            </span>
            <div className="min-w-0">
              <p className="text-sm font-semibold">Assistente IA</p>
              <p className="text-muted-foreground truncate text-xs">Pergunte sobre o seu negócio</p>
            </div>
          </div>
          <div className="flex min-h-0 flex-1 flex-col p-3">
            <AssistantChat />
          </div>
        </PopoverContent>
      </Popover>
    </div>
  );
}
