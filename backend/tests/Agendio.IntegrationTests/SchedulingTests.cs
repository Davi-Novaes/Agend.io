using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// O motor de agenda. O teste mais importante do arquivo e o de concorrencia:
/// prova que a EXCLUDE constraint do Postgres (ver migration do Scheduling), e
/// nao um lock de aplicacao, e quem garante "sem overbooking" sob concorrencia real.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class SchedulingTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Owner_Can_Schedule_An_Appointment_And_Walk_It_Through_Its_Lifecycle()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (customerId, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, cancellationToken);

        var startAtUtc = DateTimeOffset.UtcNow.AddDays(1);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc, notes = "Primeira visita" },
            cancellationToken);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var appointmentId = createBody.GetProperty("id").GetGuid();

        var getResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/appointments/{appointmentId}", cancellationToken);
        var details = await getResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        details.GetProperty("status").GetString().ShouldBe("Scheduled");
        details.GetProperty("serviceName").GetString().ShouldBe("Corte de Cabelo");
        details.GetProperty("price").GetDecimal().ShouldBe(45.90m);

        (await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}/confirm", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}/start", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}/complete", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var finalGet = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}", cancellationToken);
        var finalDetails = await finalGet.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        finalDetails.GetProperty("status").GetString().ShouldBe("Completed");
    }

    [Fact]
    public async Task Completing_An_Appointment_That_Was_Never_Started_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (customerId, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc = DateTimeOffset.UtcNow.AddDays(1), notes = (string?)null },
            cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var appointmentId = createBody.GetProperty("id").GetGuid();

        var completeResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, $"/api/appointments/{appointmentId}/complete", new { }, cancellationToken);

        completeResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Scheduling_In_The_Past_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (customerId, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc = DateTimeOffset.UtcNow.AddDays(-1), notes = (string?)null },
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Scheduling_With_An_Unknown_Resource_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (customerId, _, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId = Guid.NewGuid(), serviceId, startAtUtc = DateTimeOffset.UtcNow.AddDays(1), notes = (string?)null },
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Owner_Can_Cancel_And_Reschedule_An_Appointment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (customerId, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, cancellationToken);

        var startAtUtc = DateTimeOffset.UtcNow.AddDays(1);
        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc, notes = (string?)null },
            cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var appointmentId = createBody.GetProperty("id").GetGuid();

        var newStartAtUtc = startAtUtc.AddDays(1);
        var rescheduleResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/appointments/{appointmentId}/reschedule", new { newStartAtUtc }, cancellationToken);
        rescheduleResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterReschedule = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}", cancellationToken);
        var afterRescheduleBody = await afterReschedule.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        // Postgres timestamptz tem precisao de microssegundos; .NET DateTimeOffset
        // tem precisao de 100ns (tick) — comparar com tolerancia em vez de
        // igualdade exata evita falso-negativo por arredondamento no round-trip.
        var actualStart = afterRescheduleBody.GetProperty("startUtc").GetDateTimeOffset();
        (actualStart - newStartAtUtc).Duration().ShouldBeLessThan(TimeSpan.FromMilliseconds(5));

        var cancelResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, $"/api/appointments/{appointmentId}/cancel", new { byStaff = false }, cancellationToken);
        cancelResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterCancel = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}", cancellationToken);
        var afterCancelBody = await afterCancel.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        afterCancelBody.GetProperty("status").GetString().ShouldBe("CancelledByCustomer");
    }

    [Fact]
    public async Task Scheduling_On_A_Slot_Already_Taken_Should_Be_Rejected_With_Conflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (customerId, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, cancellationToken);

        var startAtUtc = DateTimeOffset.UtcNow.AddDays(1);

        var firstResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc, notes = (string?)null },
            cancellationToken);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var secondResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc, notes = (string?)null },
            cancellationToken);

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Bug corrigido antes de existir: sem a EXCLUDE constraint no Postgres, um
    /// "check then insert" na aplicacao deixa passar mais de uma reserva do
    /// mesmo horario sob concorrencia real. 50 requisicoes simultaneas no MESMO
    /// recurso/horario devem resultar em exatamente 1 sucesso (201) e 49
    /// rejeicoes por conflito (409) — nunca 2 ou mais sucessos.
    /// </summary>
    [Fact]
    public async Task Fifty_Concurrent_Requests_For_The_Same_Slot_Should_Yield_Exactly_One_Success()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        // 50 requisicoes verdadeiramente simultaneas no mesmo slot fazem o
        // Postgres serializar a checagem da EXCLUDE constraint entre elas —
        // correto, mas mais lento que o timeout padrao de 100s do HttpClient.
        client.Timeout = TimeSpan.FromMinutes(5);
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (customerId, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, cancellationToken);

        var startAtUtc = DateTimeOffset.UtcNow.AddDays(1);

        var tasks = Enumerable.Range(0, 50).Select(_ => AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc, notes = (string?)null },
            cancellationToken));

        var responses = await Task.WhenAll(tasks);

        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        successCount.ShouldBe(1);
        conflictCount.ShouldBe(49);
    }

    [Fact]
    public async Task A_Tenant_Should_Never_See_An_Appointment_From_Another_Tenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (customerId, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, tenantAToken, cancellationToken);
        var tenantBToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tenantAToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc = DateTimeOffset.UtcNow.AddDays(1), notes = (string?)null },
            cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var appointmentId = createBody.GetProperty("id").GetGuid();

        var crossTenantGet = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, tenantBToken, $"/api/appointments/{appointmentId}", cancellationToken);
        crossTenantGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Anonymous_Request_To_List_Appointments_Should_Be_Unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(7);
        var response = await client.GetAsync($"/api/appointments?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<(Guid CustomerId, Guid ResourceId, Guid ServiceId)> CreateBookingPrerequisitesAsync(
        HttpClient client, string accessToken, CancellationToken cancellationToken)
    {
        var customerResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/customers", new { fullName = "Cliente de Teste" }, cancellationToken);
        var customerBody = await customerResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var customerId = customerBody.GetProperty("id").GetGuid();

        var resourceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Cadeira 1", type = "Room", capacity = 1, description = (string?)null },
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
