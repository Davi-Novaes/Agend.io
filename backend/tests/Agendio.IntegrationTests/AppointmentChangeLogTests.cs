using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// Fase 14 — cancelamento e remarcacao inteligentes: motivo opcional registrado
/// num log imutavel por evento (nunca sobrescreve o agendamento em si), e
/// taxa de remarcacao agregada em GetAppointmentStats.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class AppointmentChangeLogTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Cancelling_An_Appointment_With_A_Reason_Should_Record_It_In_The_Change_Log()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (customerId, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, cancellationToken);
        var appointmentId = await ScheduleAppointmentAsync(client, accessToken, customerId, resourceId, serviceId, DateTimeOffset.UtcNow.AddDays(3), cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, $"/api/appointments/{appointmentId}/cancel",
            new { byStaff = true, reason = "Cliente pediu para cancelar" }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var historyResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/appointments/history?appointmentId={appointmentId}", cancellationToken);
        var body = await historyResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("totalCount").GetInt32().ShouldBe(1);

        var item = body.GetProperty("items")[0];
        item.GetProperty("changeType").GetString().ShouldBe("Cancelled");
        item.GetProperty("reason").GetString().ShouldBe("Cliente pediu para cancelar");
        item.GetProperty("byStaff").GetBoolean().ShouldBeTrue();
        item.GetProperty("newStartUtc").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Rescheduling_An_Appointment_Multiple_Times_Should_Keep_Each_Change_Log_Entry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (customerId, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, cancellationToken);
        var appointmentId = await ScheduleAppointmentAsync(client, accessToken, customerId, resourceId, serviceId, DateTimeOffset.UtcNow.AddDays(3), cancellationToken);

        var firstNewStart = DateTimeOffset.UtcNow.AddDays(4);
        var firstResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/appointments/{appointmentId}/reschedule",
            new { newStartAtUtc = firstNewStart, reason = "Profissional de folga" }, cancellationToken);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent, await firstResponse.Content.ReadAsStringAsync(cancellationToken));

        var secondNewStart = DateTimeOffset.UtcNow.AddDays(5);
        var secondResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/appointments/{appointmentId}/reschedule",
            new { newStartAtUtc = secondNewStart, reason = (string?)null }, cancellationToken);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent, await secondResponse.Content.ReadAsStringAsync(cancellationToken));

        var historyResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/appointments/history?appointmentId={appointmentId}", cancellationToken);
        var body = await historyResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("totalCount").GetInt32().ShouldBe(2);

        var items = body.GetProperty("items").EnumerateArray().ToList();
        items.ShouldAllBe(item => item.GetProperty("changeType").GetString() == "Rescheduled");

        var mostRecent = items[0];
        mostRecent.GetProperty("newStartUtc").GetDateTimeOffset().ShouldBe(secondNewStart, TimeSpan.FromSeconds(1));
        mostRecent.GetProperty("reason").ValueKind.ShouldBe(JsonValueKind.Null);

        var earliest = items[1];
        earliest.GetProperty("newStartUtc").GetDateTimeOffset().ShouldBe(firstNewStart, TimeSpan.FromSeconds(1));
        earliest.GetProperty("reason").GetString().ShouldBe("Profissional de folga");
    }

    [Fact]
    public async Task GetAppointmentStats_Should_Include_Reschedule_Rate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (customerId, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, cancellationToken);

        var appointment1 = await ScheduleAppointmentAsync(client, accessToken, customerId, resourceId, serviceId, DateTimeOffset.UtcNow.AddDays(3), cancellationToken);
        await ScheduleAppointmentAsync(client, accessToken, customerId, resourceId, serviceId, DateTimeOffset.UtcNow.AddDays(6), cancellationToken);

        var rescheduleResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/appointments/{appointment1}/reschedule",
            new { newStartAtUtc = DateTimeOffset.UtcNow.AddDays(4), reason = (string?)null }, cancellationToken);
        rescheduleResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent, await rescheduleResponse.Content.ReadAsStringAsync(cancellationToken));

        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var to = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var statsResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/appointments/stats?from={AuthorizedRequestHelpers.Iso(from)}&to={AuthorizedRequestHelpers.Iso(to)}", cancellationToken);
        var body = await statsResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        body.GetProperty("totalCount").GetInt32().ShouldBe(2);
        body.GetProperty("rescheduledCount").GetInt32().ShouldBe(1);
        body.GetProperty("rescheduleRate").GetDecimal().ShouldBe(50m);
    }

    [Fact]
    public async Task Appointment_Change_Log_Is_Isolated_Between_Tenants()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (customerIdA, resourceIdA, serviceIdA) = await CreateBookingPrerequisitesAsync(client, tenantAToken, cancellationToken);
        var appointmentA = await ScheduleAppointmentAsync(client, tenantAToken, customerIdA, resourceIdA, serviceIdA, DateTimeOffset.UtcNow.AddDays(3), cancellationToken);
        (await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tenantAToken, $"/api/appointments/{appointmentA}/cancel", new { byStaff = true, reason = "teste" }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var tenantBToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var historyResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, tenantBToken, "/api/appointments/history", cancellationToken);
        var body = await historyResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        body.GetProperty("totalCount").GetInt32().ShouldBe(0);
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
        HttpClient client, string accessToken, CancellationToken cancellationToken)
    {
        var customerResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/customers", new { fullName = "Cliente de Teste", email = $"cliente-{Guid.NewGuid():N}@changelogtest.com" }, cancellationToken);
        var customerBody = await customerResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var customerId = customerBody.GetProperty("id").GetGuid();

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

    private async Task<string> CreateTenantWithOwnerAndLoginAsync(HttpClient client, CancellationToken cancellationToken)
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
        await fixture.ConfirmEmailDirectlyAsync(tenantId, ownerEmail, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return loginBody.GetProperty("accessToken").GetString()!;
    }
}
