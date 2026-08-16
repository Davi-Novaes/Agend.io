using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agendio.Modules.Billing.Infrastructure.Asaas;

/// <summary>
/// Implementacao real via HttpClient tipado (ver AddBillingModule — BaseAddress
/// e headers "access_token"/"User-Agent" configurados la). Nao existe SDK .NET
/// oficial da Asaas confiavel o bastante pra depender dele; 3 chamadas REST
/// simples nao justificam trazer uma dependencia de terceiro so por isso.
/// </summary>
public sealed class AsaasClient(HttpClient httpClient) : IAsaasClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> CreateCustomerAsync(string name, string cpfCnpj, string? email, CancellationToken cancellationToken)
    {
        var request = new CreateCustomerRequest(name, cpfCnpj, email);
        using var response = await httpClient.PostAsJsonAsync("customers", request, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<CreateCustomerResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Resposta vazia da Asaas ao criar cliente.");
        return body.Id;
    }

    public async Task<AsaasNewSubscriptionResult> CreateSubscriptionAsync(
        string asaasCustomerId, decimal value, DateOnly nextDueDate, CancellationToken cancellationToken)
    {
        var request = new CreateSubscriptionRequest(
            asaasCustomerId, "UNDEFINED", value, nextDueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "MONTHLY");

        using var subscriptionResponse = await httpClient.PostAsJsonAsync("subscriptions", request, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(subscriptionResponse, cancellationToken);

        var subscriptionBody = await subscriptionResponse.Content.ReadFromJsonAsync<CreateSubscriptionResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Resposta vazia da Asaas ao criar assinatura.");

        // POST /subscriptions nao devolve invoiceUrl — a fatura so existe como
        // um Payment separado, gerado automaticamente pela assinatura.
        using var paymentsResponse = await httpClient.GetAsync(
            $"subscriptions/{subscriptionBody.Id}/payments?limit=1", cancellationToken);
        await EnsureSuccessAsync(paymentsResponse, cancellationToken);

        var paymentsBody = await paymentsResponse.Content.ReadFromJsonAsync<SubscriptionPaymentsResponse>(JsonOptions, cancellationToken);
        var firstPayment = paymentsBody?.Data.FirstOrDefault()
            ?? throw new InvalidOperationException("Assinatura criada na Asaas sem nenhum pagamento gerado.");

        return new AsaasNewSubscriptionResult(
            subscriptionBody.Id, firstPayment.Id, firstPayment.InvoiceUrl,
            DateOnly.Parse(firstPayment.DueDate, CultureInfo.InvariantCulture), firstPayment.BillingType);
    }

    public async Task CancelSubscriptionAsync(string asaasSubscriptionId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.DeleteAsync($"subscriptions/{asaasSubscriptionId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<AsaasCheckoutResult> CreateCreditCardCheckoutAsync(
        string itemName, string itemDescription, decimal value, DateOnly nextDueDate,
        string successUrl, string cancelUrl, string externalReference, CancellationToken cancellationToken)
    {
        var request = new CreateCheckoutRequest(
            ["CREDIT_CARD"],
            ["RECURRENT"],
            [new CheckoutItem(itemName, itemDescription, 1, value)],
            new CheckoutSubscription("MONTHLY", nextDueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new CheckoutCallback(successUrl, cancelUrl),
            externalReference);

        using var response = await httpClient.PostAsJsonAsync("checkouts", request, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<CreateCheckoutResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Resposta vazia da Asaas ao criar checkout.");
        return new AsaasCheckoutResult(body.Id, body.Link);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"Asaas retornou {(int)response.StatusCode}: {body}");
    }

    private sealed record CreateCustomerRequest(string Name, string CpfCnpj, string? Email);

    private sealed record CreateCustomerResponse(string Id);

    private sealed record CreateSubscriptionRequest(string Customer, string BillingType, decimal Value, string NextDueDate, string Cycle);

    private sealed record CreateSubscriptionResponse(string Id);

    private sealed record SubscriptionPaymentsResponse([property: JsonPropertyName("data")] List<SubscriptionPaymentItem> Data);

    private sealed record SubscriptionPaymentItem(string Id, string Status, decimal Value, string DueDate, string? InvoiceUrl, string BillingType);

    private sealed record CreateCheckoutRequest(
        string[] BillingTypes, string[] ChargeTypes, List<CheckoutItem> Items,
        CheckoutSubscription Subscription, CheckoutCallback Callback, string ExternalReference);

    private sealed record CheckoutItem(string Name, string Description, int Quantity, decimal Value);

    private sealed record CheckoutSubscription(string Cycle, string NextDueDate);

    private sealed record CheckoutCallback(string SuccessUrl, string CancelUrl);

    private sealed record CreateCheckoutResponse(string Id, string Link);
}
