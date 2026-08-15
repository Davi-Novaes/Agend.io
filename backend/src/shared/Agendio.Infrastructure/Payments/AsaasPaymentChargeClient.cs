using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agendio.Infrastructure.Payments;

/// <summary>
/// Implementacao real via HttpClient tipado — mesmo raciocinio de AsaasClient
/// (modulo Billing): sem SDK oficial .NET. Ao contrario da assinatura
/// (CreateSubscriptionAsync), uma cobranca avulsa (POST /payments) devolve o
/// invoiceUrl direto na mesma resposta, sem precisar de uma segunda chamada.
/// </summary>
public sealed class AsaasPaymentChargeClient(HttpClient httpClient) : IPaymentChargeClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PaymentChargeResult> CreatePixChargeAsync(
        string customerName,
        string customerCpfCnpj,
        string? customerEmail,
        decimal amount,
        string description,
        string externalReference,
        CancellationToken cancellationToken = default)
    {
        var customerId = await CreateCustomerAsync(customerName, customerCpfCnpj, customerEmail, cancellationToken);

        var paymentRequest = new CreatePaymentRequest(
            customerId, "PIX", amount, DateOnly.FromDateTime(DateTime.UtcNow), description, externalReference);

        using var response = await httpClient.PostAsJsonAsync("payments", paymentRequest, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payment = await response.Content.ReadFromJsonAsync<CreatePaymentResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Asaas nao retornou dados da cobranca criada.");

        return new PaymentChargeResult(payment.Id, payment.InvoiceUrl);
    }

    private async Task<string> CreateCustomerAsync(string name, string cpfCnpj, string? email, CancellationToken cancellationToken)
    {
        var request = new CreateCustomerRequest(name, cpfCnpj, email);
        using var response = await httpClient.PostAsJsonAsync("customers", request, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var customer = await response.Content.ReadFromJsonAsync<CreateCustomerResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Asaas nao retornou dados do cliente criado.");

        return customer.Id;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Asaas retornou {(int)response.StatusCode}: {body}");
        }
    }

    private sealed record CreateCustomerRequest(string Name, [property: JsonPropertyName("cpfCnpj")] string CpfCnpj, string? Email);

    private sealed record CreateCustomerResponse(string Id);

    private sealed record CreatePaymentRequest(
        [property: JsonPropertyName("customer")] string CustomerId,
        [property: JsonPropertyName("billingType")] string BillingType,
        decimal Value,
        [property: JsonPropertyName("dueDate")] DateOnly DueDate,
        string Description,
        [property: JsonPropertyName("externalReference")] string ExternalReference);

    private sealed record CreatePaymentResponse(string Id, [property: JsonPropertyName("invoiceUrl")] string InvoiceUrl);
}
