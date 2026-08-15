using Agendio.Infrastructure.AiAssistant;

namespace Agendio.IntegrationTests;

/// <summary>
/// Substitui o cliente de IA real (Anthropic/OpenAI/DeepSeek) nos testes de
/// integracao — nenhum provedor tem sandbox local, e testar contra a API real
/// custaria dinheiro e dependeria de rede externa (mesmo raciocinio de
/// FakeAsaasClient). Simula exatamente uma rodada de tool-calling: na primeira
/// chamada (sem resultado de ferramenta ainda na conversa) pede
/// "get_inventory_summary" com um periodo bem amplo; na segunda, ecoa o
/// resultado da ferramenta na resposta final — o suficiente para provar que os
/// dados reais dos lookups atravessam o loop do handler ate a resposta.
/// </summary>
internal sealed class FakeAiChatClient : IAiChatClient
{
    public Task<AiChatResult> SendAsync(AiChatRequest request, CancellationToken cancellationToken = default)
    {
        var lastToolResult = request.Messages.LastOrDefault(m => m.Role == AiChatRole.Tool);
        if (lastToolResult is not null)
        {
            return Task.FromResult(AiChatResult.FromText($"Resposta simulada. Dados: {lastToolResult.Text}"));
        }

        var toolCall = new AiToolCall("fake-call-1", "get_inventory_summary", """{"from":"2020-01-01","to":"2030-12-31"}""");
        return Task.FromResult(AiChatResult.FromToolCalls([toolCall]));
    }
}
