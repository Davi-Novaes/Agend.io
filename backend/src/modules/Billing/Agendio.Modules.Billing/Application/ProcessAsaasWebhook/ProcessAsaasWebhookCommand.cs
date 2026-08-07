using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Billing.Application.ProcessAsaasWebhook;

public sealed record ProcessAsaasWebhookCommand(
    string Event,
    string AsaasPaymentId,
    string? AsaasSubscriptionId,
    decimal Value,
    DateOnly DueDate,
    string? InvoiceUrl,
    string BillingType) : ICommand;
