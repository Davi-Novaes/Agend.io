namespace Agendio.Infrastructure.Security;

public sealed class TurnstileOptions
{
    public const string SectionName = "Turnstile";

    // Vazio = recurso desligado (CloudflareTurnstileVerifier pula a
    // verificacao) — dev local sem widget configurado, ou producao antes das
    // chaves reais da conta Cloudflare estarem prontas, continuam
    // funcionando normalmente em vez de travar todo login.
    public string SecretKey { get; init; } = string.Empty;
}
