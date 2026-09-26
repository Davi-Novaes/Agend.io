using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agendio.Infrastructure.Security;

public sealed class CloudflareTurnstileVerifier(
    HttpClient httpClient, IOptions<TurnstileOptions> options, ILogger<CloudflareTurnstileVerifier> logger) : ITurnstileVerifier
{
    public async Task<bool> VerifyAsync(string? token, CancellationToken cancellationToken = default)
    {
        var secretKey = options.Value.SecretKey;
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var payload = new FormUrlEncodedContent([new KeyValuePair<string, string>("secret", secretKey), new("response", token)]);

        try
        {
            var response = await httpClient.PostAsync("siteverify", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<TurnstileSiteVerifyResponse>(cancellationToken);
            return result?.Success ?? false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // API da Cloudflare fora do ar/lenta: falha fechada (nega o
            // login) em vez de propagar a excecao — mais seguro que deixar
            // passar, e o handler ja devolve um erro generico pro usuario.
            logger.LogWarning(ex, "Falha ao verificar token do Turnstile na API da Cloudflare.");
            return false;
        }
    }

    private sealed record TurnstileSiteVerifyResponse(bool Success);
}
