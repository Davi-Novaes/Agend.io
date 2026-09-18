"use client";

import * as React from "react";
import Link from "next/link";
import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import { ArrowRight, Bot, CalendarDays, HelpCircle, Package, Send, User, Users, Wallet } from "lucide-react";

import { askAssistant, ApiError, type AssistantMessage } from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { EmptyState } from "@/components/ui/empty-state";

// Categorias fixas (nao vem do catalogo do backend de proposito -- so alguns
// exemplos representativos, mais faceis de manter do que buscar/agrupar
// dinamicamente o catalogo inteiro so pra alguns botoes). "Como usar"
// demonstra a capacidade de navegacao (fast-path, resposta instantanea com
// botao "Ir para X"); "Clientes" exercita as consultas de clientes da Fase 2
// (contagem/inativos); as demais sao as perguntas de dado da Fase 1.
const SUGGESTIONS = [
  { icon: Wallet, label: "Faturamento", question: "Quanto faturei esse mes?" },
  { icon: CalendarDays, label: "Agenda", question: "Quantos agendamentos eu tive essa semana?" },
  { icon: Package, label: "Estoque", question: "Meu estoque tem algum produto acabando?" },
  { icon: Users, label: "Clientes", question: "Quantos clientes eu tenho?" },
  { icon: HelpCircle, label: "Como usar", question: "Como cadastro um cliente?" },
];

/**
 * Corpo do chat (historico + input) -- extraido da antiga pagina de tela
 * cheia /assistente pra ser embutido no popup flutuante (ver
 * components/layout/assistant-widget.tsx). Pedido explicito do usuario
 * (2026-09-06): o assistente vira um chat de canto, nao uma pagina propria.
 */
// Mensagem local, com o campo extra suggestedRoute (so faz sentido no
// cliente, pro botao "Ir para X" -- nunca faz parte do historico mandado de
// volta ao backend, que so conhece role/text, ver toHistory() abaixo).
type ChatMessage = AssistantMessage & { suggestedRoute?: string | null };

function toHistory(messages: ChatMessage[]): AssistantMessage[] {
  return messages.map(({ role, text }) => ({ role, text }));
}

export function AssistantChat() {
  const { session } = useSession();
  const accessToken = session?.accessToken ?? "";

  const [messages, setMessages] = React.useState<ChatMessage[]>([]);
  const [question, setQuestion] = React.useState("");
  const scrollAnchorRef = React.useRef<HTMLDivElement>(null);

  const askMutation = useMutation({
    mutationFn: (input: { question: string; history: AssistantMessage[] }) => askAssistant(input, accessToken),
  });

  React.useEffect(() => {
    scrollAnchorRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages, askMutation.isPending]);

  function submitQuestion(text: string) {
    const trimmed = text.trim();
    if (!trimmed || askMutation.isPending) {
      return;
    }

    const history = toHistory(messages);
    const userMessage: ChatMessage = { role: "user", text: trimmed };
    setMessages((current) => [...current, userMessage]);
    setQuestion("");

    askMutation.mutate(
      { question: trimmed, history },
      {
        onSuccess: (data) => {
          setMessages((current) => [...current, { role: "assistant", text: data.answer, suggestedRoute: data.suggestedRoute }]);
        },
        onError: (error) => {
          toast.error(error instanceof ApiError ? error.message : "Nao foi possivel falar com o assistente.");
          // Remove a pergunta que nao teve resposta, pro usuario poder tentar de novo sem duplicar.
          setMessages((current) => current.filter((m) => m !== userMessage));
          setQuestion(trimmed);
        },
      }
    );
  }

  function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    submitQuestion(question);
  }

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-3">
      <div className="flex min-h-0 flex-1 flex-col gap-3 overflow-y-auto">
        {messages.length === 0 ? (
          <EmptyState
            icon={Bot}
            title="Como posso ajudar?"
            description="Pergunte sobre faturamento, agendamentos, estoque ou avaliacoes do seu negocio."
            action={
              <div className="flex flex-wrap justify-center gap-1.5">
                {SUGGESTIONS.map(({ icon: Icon, label, question: exampleQuestion }) => (
                  <Button key={label} variant="outline" size="sm" onClick={() => submitQuestion(exampleQuestion)}>
                    <Icon className="size-3.5" />
                    {label}
                  </Button>
                ))}
              </div>
            }
          />
        ) : (
          messages.map((message, index) => (
            <div key={index} className={`flex flex-col gap-1 ${message.role === "user" ? "items-end" : "items-start"}`}>
              <div className={`flex gap-2 ${message.role === "user" ? "flex-row-reverse" : "flex-row"}`}>
                <div className="bg-muted flex size-7 shrink-0 items-center justify-center rounded-full">
                  {message.role === "user" ? <User className="size-3.5" /> : <Bot className="size-3.5" />}
                </div>
                <div
                  className={`max-w-[80%] rounded-lg px-3 py-1.5 text-sm whitespace-pre-wrap ${
                    message.role === "user" ? "bg-primary text-primary-foreground" : "bg-muted"
                  }`}
                >
                  {message.text}
                </div>
              </div>
              {message.suggestedRoute ? (
                <Button asChild variant="link" size="sm" className="ml-9 h-auto p-0 text-xs">
                  <Link href={message.suggestedRoute}>
                    Ir para lá
                    <ArrowRight className="size-3" />
                  </Link>
                </Button>
              ) : null}
            </div>
          ))
        )}
        {/* Mantido tambem pro fast-path (Fase 3): a resposta e instantanea no
            backend, mas ainda passa por uma requisicao HTTP real -- esconder
            o indicador so pra esse caso exigiria saber de antemao (antes da
            resposta chegar) se vai cair no fast-path, o que o cliente nao
            sabe. */}
        {askMutation.isPending ? (
          <div className="flex gap-2">
            <div className="bg-muted flex size-7 shrink-0 items-center justify-center rounded-full">
              <Bot className="size-3.5" />
            </div>
            <div className="bg-muted rounded-lg px-3 py-1.5 text-sm">Pensando...</div>
          </div>
        ) : null}
        <div ref={scrollAnchorRef} />
      </div>

      <form onSubmit={handleSubmit} className="flex gap-2">
        <Textarea
          value={question}
          onChange={(event) => setQuestion(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter" && !event.shiftKey) {
              event.preventDefault();
              submitQuestion(question);
            }
          }}
          placeholder="Pergunte algo sobre o seu negocio..."
          rows={2}
          className="resize-none"
        />
        <Button type="submit" disabled={askMutation.isPending || !question.trim()}>
          <Send className="size-4" />
        </Button>
      </form>
    </div>
  );
}
