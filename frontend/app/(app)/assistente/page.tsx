"use client";

import * as React from "react";
import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import { Bot, Send, User } from "lucide-react";

import { askAssistant, ApiError, type AssistantMessage } from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";

const EXAMPLE_QUESTIONS = [
  "Quanto faturei esse mes?",
  "Quantos agendamentos eu tive essa semana?",
  "Meu estoque tem algum produto acabando?",
  "Qual minha taxa de faltas esse mes?",
];

export default function AssistantePage() {
  const { session } = useSession();
  const accessToken = session?.accessToken ?? "";

  const [messages, setMessages] = React.useState<AssistantMessage[]>([]);
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

    const history = messages;
    const userMessage: AssistantMessage = { role: "user", text: trimmed };
    setMessages((current) => [...current, userMessage]);
    setQuestion("");

    askMutation.mutate(
      { question: trimmed, history },
      {
        onSuccess: (data) => {
          setMessages((current) => [...current, { role: "assistant", text: data.answer }]);
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
    <div className="mx-auto flex w-full max-w-3xl flex-1 flex-col">
      <p className="text-muted-foreground mb-6 text-sm">
        Pergunte em linguagem natural sobre os dados do seu negocio — financeiro, agendamentos, estoque e avaliacoes.
      </p>

      <Card className="flex flex-1 flex-col">
        <CardContent className="flex flex-1 flex-col gap-4">
          <div className="flex min-h-[320px] flex-1 flex-col gap-4 overflow-y-auto">
            {messages.length === 0 ? (
              <EmptyState
                icon={Bot}
                title="Como posso ajudar?"
                description="Pergunte sobre faturamento, agendamentos, estoque ou avaliacoes do seu negocio."
                action={
                  <div className="flex flex-wrap justify-center gap-2">
                    {EXAMPLE_QUESTIONS.map((example) => (
                      <Button key={example} variant="outline" size="sm" onClick={() => submitQuestion(example)}>
                        {example}
                      </Button>
                    ))}
                  </div>
                }
              />
            ) : (
              messages.map((message, index) => (
                <div
                  key={index}
                  className={`flex gap-3 ${message.role === "user" ? "flex-row-reverse" : "flex-row"}`}
                >
                  <div className="bg-muted flex size-8 shrink-0 items-center justify-center rounded-full">
                    {message.role === "user" ? <User className="size-4" /> : <Bot className="size-4" />}
                  </div>
                  <div
                    className={`max-w-[80%] rounded-lg px-4 py-2 text-sm whitespace-pre-wrap ${
                      message.role === "user" ? "bg-primary text-primary-foreground" : "bg-muted"
                    }`}
                  >
                    {message.text}
                  </div>
                </div>
              ))
            )}
            {askMutation.isPending ? (
              <div className="flex gap-3">
                <div className="bg-muted flex size-8 shrink-0 items-center justify-center rounded-full">
                  <Bot className="size-4" />
                </div>
                <div className="bg-muted rounded-lg px-4 py-2 text-sm">Pensando...</div>
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
        </CardContent>
      </Card>
    </div>
  );
}
