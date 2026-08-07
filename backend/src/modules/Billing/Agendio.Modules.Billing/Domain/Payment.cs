using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Billing.Domain;

/// <summary>
/// Um registro de cobranca gerado pela Asaas (uma "fatura"). Mesma excecao de
/// ITenantOwned/RLS que Subscription — ver comentario la.
/// AsaasPaymentId e a chave de idempotencia: o webhook faz upsert por ela,
/// entao reentrega do mesmo evento pela Asaas nunca duplica uma linha.
/// </summary>
public sealed class Payment : AggregateRoot<PaymentId>, IAuditable
{
    public TenantId TenantId { get; private set; } = null!;

    public SubscriptionId SubscriptionId { get; private set; } = null!;

    public string AsaasPaymentId { get; private set; } = string.Empty;

    public PaymentStatus Status { get; private set; }

    public decimal Amount { get; private set; }

    public DateOnly DueDate { get; private set; }

    public DateTimeOffset? PaidAtUtc { get; private set; }

    public string? InvoiceUrl { get; private set; }

    public string BillingType { get; private set; } = "UNDEFINED";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    private Payment()
    {
    }

    public Payment(
        TenantId tenantId, SubscriptionId subscriptionId, string asaasPaymentId,
        decimal amount, DateOnly dueDate, string? invoiceUrl, string billingType)
        : base(PaymentId.New())
    {
        TenantId = tenantId;
        SubscriptionId = subscriptionId;
        AsaasPaymentId = asaasPaymentId;
        Status = PaymentStatus.Pending;
        Amount = amount;
        DueDate = dueDate;
        InvoiceUrl = invoiceUrl;
        BillingType = billingType;
    }

    public void MarkConfirmed(DateTimeOffset paidAtUtc)
    {
        Status = PaymentStatus.Confirmed;
        PaidAtUtc = paidAtUtc;
    }

    public void MarkOverdue() => Status = PaymentStatus.Overdue;

    public void MarkRefunded() => Status = PaymentStatus.Refunded;

    public void UpdateFromWebhook(string? invoiceUrl, string billingType)
    {
        InvoiceUrl = invoiceUrl ?? InvoiceUrl;
        BillingType = billingType;
    }
}
