using Agendio.Modules.Billing.Infrastructure.Asaas;

namespace Agendio.IntegrationTests;

/// <summary>
/// Substitui AsaasClient nos testes de integracao — a Asaas e um servico
/// hospedado sem container local equivalente (ao contrario de Postgres/Redis/
/// RabbitMQ/MailHog), entao nao da pra testar contra o sandbox real em CI.
/// Ids deterministicos, sempre "sucesso" — o objetivo aqui e testar a MAQUINA
/// DE ESTADOS local (Subscription/Payment reagindo a webhook), nao a
/// integracao HTTP em si.
/// </summary>
internal sealed class FakeAsaasClient : IAsaasClient
{
    public Task<string> CreateCustomerAsync(string name, string cpfCnpj, string? email, CancellationToken cancellationToken) =>
        Task.FromResult($"fake-cus-{Guid.NewGuid():N}");

    public Task<AsaasNewSubscriptionResult> CreateSubscriptionAsync(string asaasCustomerId, decimal value, DateOnly nextDueDate, CancellationToken cancellationToken) =>
        Task.FromResult(new AsaasNewSubscriptionResult(
            $"fake-sub-{Guid.NewGuid():N}", $"fake-pay-{Guid.NewGuid():N}", "https://fake.local/checkout", nextDueDate, "UNDEFINED"));

    public Task CancelSubscriptionAsync(string asaasSubscriptionId, CancellationToken cancellationToken) => Task.CompletedTask;
}
