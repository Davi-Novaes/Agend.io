namespace Agendio.Modules.Billing.Infrastructure.Asaas;

/// <summary>Camada fina sobre a API v3 da Asaas — so os metodos que o modulo Billing precisa.</summary>
public interface IAsaasClient
{
    Task<string> CreateCustomerAsync(string name, string cpfCnpj, string? email, CancellationToken cancellationToken);

    Task<AsaasNewSubscriptionResult> CreateSubscriptionAsync(string asaasCustomerId, decimal value, DateOnly nextDueDate, CancellationToken cancellationToken);

    Task CancelSubscriptionAsync(string asaasSubscriptionId, CancellationToken cancellationToken);

    /// <summary>
    /// POST /v3/checkouts sem "customer"/"customerData" de proposito: passar
    /// dado parcial do titular (ex.: so nome/CPF/e-mail, sem endereco) e
    /// rejeitado pela Asaas ("todo ou nada"), enquanto omitir por completo
    /// deixa a propria pagina hospedada coletar nome/CPF-CNPJ/telefone/endereco
    /// do pagador — evita pedir esses campos no onboarding (contrato validado
    /// direto na sandbox, ver Fase 24). "callback" e obrigatorio e exige uma
    /// URL https com dominio bem formado (localhost/http sao rejeitados, mas
    /// nao precisa resolver de verdade).
    /// </summary>
    Task<AsaasCheckoutResult> CreateCreditCardCheckoutAsync(
        string itemName, string itemDescription, decimal value, DateOnly nextDueDate,
        string successUrl, string cancelUrl, string externalReference, CancellationToken cancellationToken);
}

/// <summary>
/// billingType vem "UNDEFINED" ate o pagador escolher PIX/boleto/cartao na
/// pagina hospedada da Asaas (invoiceUrl) — atualizado depois via webhook.
/// </summary>
public sealed record AsaasNewSubscriptionResult(
    string AsaasSubscriptionId, string AsaasPaymentId, string? InvoiceUrl, DateOnly DueDate, string BillingType);

/// <summary>Link (pagina hospedada da Asaas) para abrir numa nova aba — o pagador preenche cartao e dados la, nunca no nosso dominio.</summary>
public sealed record AsaasCheckoutResult(string CheckoutId, string Link);
