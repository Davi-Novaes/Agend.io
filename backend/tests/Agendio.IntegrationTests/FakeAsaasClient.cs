using System.Collections.Concurrent;
using Agendio.Modules.Billing.Infrastructure.Asaas;

namespace Agendio.IntegrationTests;

/// <summary>
/// Substitui AsaasClient nos testes de integracao — a Asaas e um servico
/// hospedado sem container local equivalente (ao contrario de Postgres/Redis/
/// RabbitMQ/MailHog), entao nao da pra testar contra o sandbox real em CI.
/// Ids deterministicos, sempre "sucesso" — o objetivo aqui e testar a MAQUINA
/// DE ESTADOS local (Subscription/Payment reagindo a webhook), nao a
/// integracao HTTP em si. Registrado como singleton (ver IntegrationTestFixture),
/// entao o dicionario de pagamentos por assinatura sobrevive entre requisicoes
/// HTTP diferentes do mesmo teste — precisa pra simular GET /subscriptions/{id}/
/// payments devolvendo o MESMO pagamento que POST /subscriptions criou, do
/// jeito que a Asaas real faria.
/// </summary>
internal sealed class FakeAsaasClient : IAsaasClient
{
    private readonly ConcurrentDictionary<string, string> _paymentIdBySubscriptionId = new();

    public Task<string> CreateCustomerAsync(string name, string cpfCnpj, string? email, CancellationToken cancellationToken) =>
        Task.FromResult($"fake-cus-{Guid.NewGuid():N}");

    public Task<AsaasNewSubscriptionResult> CreateSubscriptionAsync(string asaasCustomerId, decimal value, DateOnly nextDueDate, CancellationToken cancellationToken)
    {
        var subscriptionId = $"fake-sub-{Guid.NewGuid():N}";
        var paymentId = _paymentIdBySubscriptionId.GetOrAdd(subscriptionId, _ => $"fake-pay-{Guid.NewGuid():N}");
        return Task.FromResult(new AsaasNewSubscriptionResult(subscriptionId, paymentId, "https://fake.local/checkout", nextDueDate, "UNDEFINED"));
    }

    public Task<AsaasNewSubscriptionResult> GetLatestSubscriptionPaymentAsync(string asaasSubscriptionId, CancellationToken cancellationToken)
    {
        var paymentId = _paymentIdBySubscriptionId.GetOrAdd(asaasSubscriptionId, _ => $"fake-pay-{Guid.NewGuid():N}");
        return Task.FromResult(new AsaasNewSubscriptionResult(
            asaasSubscriptionId, paymentId, "https://fake.local/checkout", DateOnly.FromDateTime(DateTime.UtcNow), "UNDEFINED"));
    }

    public Task CancelSubscriptionAsync(string asaasSubscriptionId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<AsaasCheckoutResult> CreateCreditCardCheckoutAsync(
        string itemName, string itemDescription, decimal value, DateOnly nextDueDate,
        string successUrl, string cancelUrl, string externalReference, CancellationToken cancellationToken) =>
        Task.FromResult(new AsaasCheckoutResult($"fake-checkout-{Guid.NewGuid():N}", "https://fake.local/checkout-session"));
}
