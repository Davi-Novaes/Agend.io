namespace Agendio.Modules.Billing.Infrastructure.Asaas;

/// <summary>Config da integracao com a Asaas. ApiKey/WebhookSecret sao segredos — appsettings.Development.json em dev, nunca commitados com valor de producao (mesmo tratamento de Jwt:SigningKey).</summary>
public sealed class AsaasOptions
{
    public const string SectionName = "Asaas";

    /// <summary>Sandbox: https://api-sandbox.asaas.com/v3 · Producao: https://api.asaas.com/v3</summary>
    public required string BaseUrl { get; init; }

    public required string ApiKey { get; init; }

    /// <summary>Comparado contra o header "asaas-access-token" de todo webhook recebido — Asaas nao assina o corpo, e um token estatico configurado no painel da Asaas.</summary>
    public required string WebhookSecret { get; init; }
}
