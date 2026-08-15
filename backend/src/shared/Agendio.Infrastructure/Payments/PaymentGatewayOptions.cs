namespace Agendio.Infrastructure.Payments;

/// <summary>
/// Mesma conta/secao "Asaas" que o modulo Billing usa (AsaasOptions) — BaseUrl
/// e ApiKey sao o mesmo par de credenciais, so o webhook de deposito de
/// agendamento tem um segredo proprio (webhook separado da assinatura,
/// configurado a parte no painel da Asaas).
/// </summary>
public sealed class PaymentGatewayOptions
{
    public const string SectionName = "Asaas";

    public required string BaseUrl { get; init; }

    public required string ApiKey { get; init; }

    /// <summary>Comparado contra o header "asaas-access-token" do webhook de deposito de agendamento (Fase 16).</summary>
    public required string AppointmentDepositWebhookSecret { get; init; }
}
