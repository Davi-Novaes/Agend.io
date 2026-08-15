namespace Agendio.Infrastructure.Payments;

/// <summary>
/// Cobranca avulsa (PIX) via gateway de pagamento — distinto de IAsaasClient
/// (modulo Billing), que so trata assinatura recorrente da plataforma. Global
/// (mesma conta Asaas da Agendio para todos os tenants, sem split de
/// pagamento), por isso a config vem de IOptions (comparar com IWhatsAppSender,
/// que e por tenant).
/// </summary>
public interface IPaymentChargeClient
{
    Task<PaymentChargeResult> CreatePixChargeAsync(
        string customerName,
        string customerCpfCnpj,
        string? customerEmail,
        decimal amount,
        string description,
        string externalReference,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentChargeResult(string ChargeId, string InvoiceUrl);
