using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agendio.Infrastructure.Notifications;

/// <summary>
/// Implementacao real via HttpClient tipado contra a WhatsApp Cloud API (Meta) —
/// mesmo raciocinio de AsaasClient: sem SDK oficial .NET confiavel o bastante
/// pra justificar a dependencia por uma unica chamada REST. BaseAddress vem de
/// WhatsAppOptions (AddHttpClient, ver InfrastructureServiceCollectionExtensions);
/// credenciais (numero + token) mudam por chamada porque sao POR TENANT.
/// </summary>
public sealed class WhatsAppCloudApiSender(HttpClient httpClient) : IWhatsAppSender
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SendAsync(WhatsAppCredentials credentials, string toPhoneE164, string message, CancellationToken cancellationToken = default)
    {
        var toDigitsOnly = toPhoneE164.TrimStart('+');
        var request = new SendMessageRequest("whatsapp", toDigitsOnly, "text", new SendMessageText(message));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{credentials.PhoneNumberId}/messages")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credentials.AccessToken);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"WhatsApp Cloud API retornou {(int)response.StatusCode}: {body}");
        }
    }

    private sealed record SendMessageRequest(
        [property: JsonPropertyName("messaging_product")] string MessagingProduct,
        string To,
        string Type,
        [property: JsonPropertyName("text")] SendMessageText Text);

    private sealed record SendMessageText(string Body);
}
