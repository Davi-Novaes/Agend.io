using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// Fase 12 — avaliacoes: submissao publica confirmando identidade por e-mail
/// (sem token/link secreto, mesmo espirito anti-enumeracao da Fase 11), regra
/// de negocio (so agendamento Completed, no maximo um review), resumo agregado
/// (media/evolucao/por servico/por profissional) e isolamento cruzado de tenant.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class ReviewTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Submitting_A_Review_For_A_Completed_Appointment_Should_Succeed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantId = await GetTenantIdFromTokenAsync(accessToken);

        var (customerId, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, "cliente@reviewtest.com", cancellationToken);
        var appointmentId = await ScheduleAndCompleteAsync(client, accessToken, customerId, resourceId, serviceId, cancellationToken);

        var response = await client.PostAsJsonAsync(
            $"/api/public/tenants/{tenantId}/appointments/{appointmentId}/review",
            new { customerEmail = "cliente@reviewtest.com", rating = 5, comment = "Otimo atendimento!" },
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Submitting_A_Review_With_Wrong_Email_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantId = await GetTenantIdFromTokenAsync(accessToken);

        var (customerId, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, "cliente@reviewtest.com", cancellationToken);
        var appointmentId = await ScheduleAndCompleteAsync(client, accessToken, customerId, resourceId, serviceId, cancellationToken);

        var response = await client.PostAsJsonAsync(
            $"/api/public/tenants/{tenantId}/appointments/{appointmentId}/review",
            new { customerEmail = "outro@example.com", rating = 5, comment = (string?)null },
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Submitting_A_Review_For_A_Not_Completed_Appointment_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantId = await GetTenantIdFromTokenAsync(accessToken);

        var (customerId, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, "cliente@reviewtest.com", cancellationToken);
        var appointmentId = await ScheduleAppointmentAsync(client, accessToken, customerId, resourceId, serviceId, cancellationToken);

        var response = await client.PostAsJsonAsync(
            $"/api/public/tenants/{tenantId}/appointments/{appointmentId}/review",
            new { customerEmail = "cliente@reviewtest.com", rating = 4, comment = (string?)null },
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Submitting_A_Duplicate_Review_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantId = await GetTenantIdFromTokenAsync(accessToken);

        var (customerId, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, "cliente@reviewtest.com", cancellationToken);
        var appointmentId = await ScheduleAndCompleteAsync(client, accessToken, customerId, resourceId, serviceId, cancellationToken);

        var reviewUrl = $"/api/public/tenants/{tenantId}/appointments/{appointmentId}/review";
        var payload = new { customerEmail = "cliente@reviewtest.com", rating = 5, comment = (string?)null };

        (await client.PostAsJsonAsync(reviewUrl, payload, cancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PostAsJsonAsync(reviewUrl, payload, cancellationToken)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetReviewsSummary_Should_Aggregate_Average_By_Service_And_Professional()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantId = await GetTenantIdFromTokenAsync(accessToken);

        var (customer1Id, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, "cliente1@reviewtest.com", cancellationToken);
        var appointment1 = await ScheduleAndCompleteAsync(client, accessToken, customer1Id, resourceId, serviceId, cancellationToken);

        var customer2Id = await CreateCustomerAsync(client, accessToken, "cliente2@reviewtest.com", cancellationToken);
        var appointment2 = await ScheduleAppointmentAsync(client, accessToken, customer2Id, resourceId, serviceId, cancellationToken);
        await CompleteAppointmentAsync(client, accessToken, appointment2, cancellationToken);

        var review1Response = await client.PostAsJsonAsync(
            $"/api/public/tenants/{tenantId}/appointments/{appointment1}/review",
            new { customerEmail = "cliente1@reviewtest.com", rating = 5, comment = (string?)null }, cancellationToken);
        review1Response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await review1Response.Content.ReadAsStringAsync(cancellationToken));

        var review2Response = await client.PostAsJsonAsync(
            $"/api/public/tenants/{tenantId}/appointments/{appointment2}/review",
            new { customerEmail = "cliente2@reviewtest.com", rating = 3, comment = "Poderia ser melhor" }, cancellationToken);
        review2Response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await review2Response.Content.ReadAsStringAsync(cancellationToken));

        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var to = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var response = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/appointments/reviews/summary?from={AuthorizedRequestHelpers.Iso(from)}&to={AuthorizedRequestHelpers.Iso(to)}", cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("totalCount").GetInt32().ShouldBe(2);
        body.GetProperty("averageRating").GetDecimal().ShouldBe(4m);

        var byService = body.GetProperty("byService");
        byService.GetArrayLength().ShouldBe(1);
        byService[0].GetProperty("count").GetInt32().ShouldBe(2);

        var byProfessional = body.GetProperty("byProfessional");
        byProfessional.GetArrayLength().ShouldBe(1);
        byProfessional[0].GetProperty("count").GetInt32().ShouldBe(2);

        var recentReviews = body.GetProperty("recentReviews");
        recentReviews.GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Reviews_Are_Isolated_Between_Tenants()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (customerIdA, resourceIdA, serviceIdA) = await CreateBookingPrerequisitesAsync(client, tenantAToken, "cliente@reviewtest.com", cancellationToken);
        var appointmentA = await ScheduleAndCompleteAsync(client, tenantAToken, customerIdA, resourceIdA, serviceIdA, cancellationToken);
        var tenantAId = await GetTenantIdFromTokenAsync(tenantAToken);
        await client.PostAsJsonAsync(
            $"/api/public/tenants/{tenantAId}/appointments/{appointmentA}/review",
            new { customerEmail = "cliente@reviewtest.com", rating = 5, comment = (string?)null }, cancellationToken);

        var tenantBToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var to = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var response = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, tenantBToken, $"/api/appointments/reviews/summary?from={AuthorizedRequestHelpers.Iso(from)}&to={AuthorizedRequestHelpers.Iso(to)}", cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    private static async Task<Guid> ScheduleAndCompleteAsync(
        HttpClient client, string accessToken, Guid customerId, Guid resourceId, Guid serviceId, CancellationToken cancellationToken)
    {
        var appointmentId = await ScheduleAppointmentAsync(client, accessToken, customerId, resourceId, serviceId, cancellationToken);
        await CompleteAppointmentAsync(client, accessToken, appointmentId, cancellationToken);
        return appointmentId;
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
        HttpClient client, string accessToken, Guid customerId, Guid resourceId, Guid serviceId, CancellationToken cancellationToken)
    {
        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc = DateTimeOffset.UtcNow.AddDays(1), notes = (string?)null },
            cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task CompleteAppointmentAsync(HttpClient client, string accessToken, Guid appointmentId, CancellationToken cancellationToken)
    {
        (await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}/confirm", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}/start", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}/complete", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
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
