using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// Fase 13 — lista de espera: cliente entra pelo portal publico quando nao ha
/// horario disponivel, a equipe e notificada quando um agendamento compativel
/// (mesmo servico/data/opcionalmente mesmo recurso) e cancelado, e confirma
/// manualmente convertendo uma das entradas notificadas em agendamento.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class WaitlistTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Joining_The_Waitlist_Should_Reuse_The_Same_Customer_By_Email()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantId = await GetTenantIdFromTokenAsync(accessToken);
        var (_, _, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, "espera@waitlisttest.com", cancellationToken);

        var preferredDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var payload = new
        {
            serviceId, resourceId = (Guid?)null, preferredDate, customerFullName = "Cliente Espera",
            customerEmail = "espera@waitlisttest.com", customerPhone = (string?)null, notes = (string?)null,
        };

        var response1 = await client.PostAsJsonAsync($"/api/public/tenants/{tenantId}/waitlist", payload, cancellationToken);
        response1.StatusCode.ShouldBe(HttpStatusCode.Created);
        var response2 = await client.PostAsJsonAsync($"/api/public/tenants/{tenantId}/waitlist", payload, cancellationToken);
        response2.StatusCode.ShouldBe(HttpStatusCode.Created);

        var listResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/waitlist?page=1&pageSize=20", cancellationToken);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var items = listBody.GetProperty("items").EnumerateArray().ToList();

        items.Count.ShouldBe(2);
        items[0].GetProperty("customerId").GetGuid().ShouldBe(items[1].GetProperty("customerId").GetGuid());
    }

    [Fact]
    public async Task Joining_The_Waitlist_With_A_Past_Date_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantId = await GetTenantIdFromTokenAsync(accessToken);
        var (_, _, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, "espera@waitlisttest.com", cancellationToken);

        var response = await client.PostAsJsonAsync($"/api/public/tenants/{tenantId}/waitlist", new
        {
            serviceId, resourceId = (Guid?)null, preferredDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
            customerFullName = "Cliente Espera", customerEmail = "espera@waitlisttest.com", customerPhone = (string?)null, notes = (string?)null,
        }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancelling_A_Waitlist_Entry_Should_Prevent_Converting_It()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantId = await GetTenantIdFromTokenAsync(accessToken);
        var (_, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, "espera@waitlisttest.com", cancellationToken);

        var entryId = await JoinWaitlistAsync(client, tenantId, serviceId, null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), "espera@waitlisttest.com", cancellationToken);

        (await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/waitlist/{entryId}/cancel", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var convertResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, $"/api/waitlist/{entryId}/convert",
            new { resourceId, startAtUtc = DateTimeOffset.UtcNow.AddDays(5) }, cancellationToken);
        convertResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var listResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/waitlist?status=Cancelled", cancellationToken);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        listBody.GetProperty("totalCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Converting_A_Waitlist_Entry_Should_Create_An_Appointment_And_Mark_It_Converted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantId = await GetTenantIdFromTokenAsync(accessToken);
        var (_, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, "espera@waitlisttest.com", cancellationToken);

        var entryId = await JoinWaitlistAsync(client, tenantId, serviceId, null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), "espera@waitlisttest.com", cancellationToken);

        var convertResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, $"/api/waitlist/{entryId}/convert",
            new { resourceId, startAtUtc = DateTimeOffset.UtcNow.AddDays(5) }, cancellationToken);
        convertResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await convertResponse.Content.ReadAsStringAsync(cancellationToken));
        var convertBody = await convertResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var appointmentId = convertBody.GetProperty("id").GetGuid();

        var appointmentResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}", cancellationToken);
        var appointmentBody = await appointmentResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        appointmentBody.GetProperty("status").GetString().ShouldBe("Scheduled");

        var listResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/waitlist?status=Converted", cancellationToken);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        listBody.GetProperty("totalCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Cancelling_An_Appointment_Should_Notify_Matching_Waitlist_Entries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantId = await GetTenantIdFromTokenAsync(accessToken);
        var (customerId, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, "dono@waitlisttest.com", cancellationToken);

        // Meio-dia UTC evita virada de dia ao converter para o fuso do tenant (America/Sao_Paulo, UTC-3).
        var startAtUtc = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(5).AddHours(15), TimeSpan.Zero);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var preferredDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(startAtUtc, timeZone).DateTime);

        var appointmentId = await ScheduleAppointmentAsync(client, accessToken, customerId, resourceId, serviceId, startAtUtc, cancellationToken);

        var entryId = await JoinWaitlistAsync(client, tenantId, serviceId, null, preferredDate, "espera@waitlisttest.com", cancellationToken);

        (await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, $"/api/appointments/{appointmentId}/cancel", new { byStaff = true }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var found = await PollMailHogForAsync("espera@waitlisttest.com", cancellationToken, subjectContains: "Uma vaga abriu");
        found.ShouldBeTrue();

        var listResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/waitlist?status=Notified", cancellationToken);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        listBody.GetProperty("totalCount").GetInt32().ShouldBe(1);
        listBody.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldBe(entryId);
    }

    [Fact]
    public async Task Waitlist_Entries_Are_Isolated_Between_Tenants()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantAId = await GetTenantIdFromTokenAsync(tenantAToken);
        var (_, _, serviceIdA) = await CreateBookingPrerequisitesAsync(client, tenantAToken, "espera@waitlisttest.com", cancellationToken);
        await JoinWaitlistAsync(client, tenantAId, serviceIdA, null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), "espera@waitlisttest.com", cancellationToken);

        var tenantBToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var listResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, tenantBToken, "/api/waitlist", cancellationToken);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        listBody.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    private async Task<bool> PollMailHogForAsync(string mustContain, CancellationToken cancellationToken, string? subjectContains = null)
    {
        using var mailHogClient = new HttpClient { BaseAddress = new Uri(fixture.MailHogApiBaseUrl) };
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var messagesJson = await mailHogClient.GetStringAsync("/api/v2/messages", cancellationToken);
            if (messagesJson.Contains(mustContain, StringComparison.OrdinalIgnoreCase)
                && (subjectContains is null || messagesJson.Contains(subjectContains, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            await Task.Delay(300, cancellationToken);
        }

        return false;
    }

    private static async Task<Guid> JoinWaitlistAsync(
        HttpClient client, Guid tenantId, Guid serviceId, Guid? resourceId, DateOnly preferredDate, string customerEmail, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync($"/api/public/tenants/{tenantId}/waitlist", new
        {
            serviceId, resourceId, preferredDate, customerFullName = "Cliente Espera", customerEmail, customerPhone = (string?)null, notes = (string?)null,
        }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(cancellationToken));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private static Task<Guid> GetTenantIdFromTokenAsync(string accessToken)
    {
        var payloadSegment = accessToken.Split('.')[1];
        var padded = payloadSegment.PadRight(payloadSegment.Length + (4 - payloadSegment.Length % 4) % 4, '=');
        var payloadJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/')));
        var payload = JsonDocument.Parse(payloadJson).RootElement;

        return Task.FromResult(payload.GetProperty("tenant_id").GetGuid());
    }

    private static async Task<Guid> CreateCustomerAsync(HttpClient client, string accessToken, string email, CancellationToken cancellationToken)
    {
        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/customers", new { fullName = "Cliente de Teste", email }, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> ScheduleAppointmentAsync(
        HttpClient client, string accessToken, Guid customerId, Guid resourceId, Guid serviceId, DateTimeOffset startAtUtc, CancellationToken cancellationToken)
    {
        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc, notes = (string?)null },
            cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(cancellationToken));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<(Guid CustomerId, Guid ResourceId, Guid ServiceId)> CreateBookingPrerequisitesAsync(
        HttpClient client, string accessToken, string customerEmail, CancellationToken cancellationToken)
    {
        var customerId = await CreateCustomerAsync(client, accessToken, customerEmail, cancellationToken);

        var resourceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Barbeiro 1", type = "Person", capacity = 1, description = (string?)null },
            cancellationToken);
        var resourceBody = await resourceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var resourceId = resourceBody.GetProperty("id").GetGuid();

        var serviceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/services",
            new { name = "Corte de Cabelo", description = (string?)null, durationMinutes = 30, price = 45.90m, currency = "BRL", category = (string?)null },
            cancellationToken);
        var serviceBody = await serviceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var serviceId = serviceBody.GetProperty("id").GetGuid();

        return (customerId, resourceId, serviceId);
    }

    private static async Task<string> CreateTenantWithOwnerAndLoginAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var tenantResponse = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = $"Tenant {Guid.NewGuid():N}",
            slug = $"tenant-{Guid.NewGuid():N}",
            businessType = "Other",
            timeZoneId = "America/Sao_Paulo",
        }, cancellationToken);
        tenantResponse.EnsureSuccessStatusCode();
        var tenantBody = await tenantResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var tenantId = tenantBody.GetProperty("id").GetGuid();

        var ownerEmail = $"owner-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync(
            "/api/auth/register", new { tenantId, email = ownerEmail, password = Password, fullName = "Dono" }, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return loginBody.GetProperty("accessToken").GetString()!;
    }
}
