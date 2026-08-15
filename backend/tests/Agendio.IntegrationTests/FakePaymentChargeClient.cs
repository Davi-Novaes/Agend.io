using Agendio.Infrastructure.Payments;

namespace Agendio.IntegrationTests;

/// <summary>
/// Substitui AsaasPaymentChargeClient nos testes de integracao — mesmo
/// raciocinio de FakeAsaasClient (ver esse arquivo): a Asaas e um servico
/// hospedado, sem sandbox acessivel em CI. Sempre "sucesso", id deterministico.
/// </summary>
internal sealed class FakePaymentChargeClient : IPaymentChargeClient
{
    public Task<PaymentChargeResult> CreatePixChargeAsync(
        string customerName, string customerCpfCnpj, string? customerEmail, decimal amount, string description,
        string externalReference, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PaymentChargeResult($"fake-charge-{Guid.NewGuid():N}", "https://fake.local/pagamento"));
}
