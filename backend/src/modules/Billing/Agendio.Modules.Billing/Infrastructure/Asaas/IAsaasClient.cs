namespace Agendio.Modules.Billing.Infrastructure.Asaas;

/// <summary>Camada fina sobre a API v3 da Asaas — so os 3 metodos que o modulo Billing precisa.</summary>
public interface IAsaasClient
{
    Task<string> CreateCustomerAsync(string name, string cpfCnpj, string? email, CancellationToken cancellationToken);

    Task<AsaasNewSubscriptionResult> CreateSubscriptionAsync(string asaasCustomerId, decimal value, DateOnly nextDueDate, CancellationToken cancellationToken);

    Task CancelSubscriptionAsync(string asaasSubscriptionId, CancellationToken cancellationToken);
}

/// <summary>
/// billingType vem "UNDEFINED" ate o pagador escolher PIX/boleto/cartao na
/// pagina hospedada da Asaas (invoiceUrl) — atualizado depois via webhook.
/// </summary>
public sealed record AsaasNewSubscriptionResult(
    string AsaasSubscriptionId, string AsaasPaymentId, string? InvoiceUrl, DateOnly DueDate, string BillingType);
