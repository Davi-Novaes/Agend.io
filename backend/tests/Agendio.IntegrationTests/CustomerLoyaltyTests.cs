using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendio.Infrastructure.Messaging;
using Agendio.Modules.Customers.Domain;
using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.IntegrationTests;

/// <summary>
/// Fase 11 — programa de fidelidade: credito automatico de 1 ponto por visita
/// concluida (via LoyaltyIntegrationEventConsumer, mesmo desenho do
/// FinancialIntegrationEventConsumer — ver FinanceiroTests.cs), resgate de
/// recompensa pelo dono, consulta publica por e-mail (sem login) e isolamento
/// cruzado de tenant.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class CustomerLoyaltyTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Completing_An_Appointment_Should_Credit_One_Loyalty_Point_Automatically()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (customerId, _, appointmentId, tenantId) = await CreateAndCompleteAppointmentAsync(client, accessToken, "cliente@example.com", cancellationToken);

        var ledgerEntry = await PollAsync(
            tenantId,
            dbContext => dbContext.LoyaltyPointsLedgerEntries.SingleOrDefaultAsync(e => e.AppointmentId == appointmentId, cancellationToken),
            cancellationToken);

        ledgerEntry.ShouldNotBeNull();
        ledgerEntry.Kind.ShouldBe(LoyaltyPointsLedgerEntryKind.Earned);
        ledgerEntry.Points.ShouldBe(1);

        await using var scope = fixture.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.SetTenant(TenantId.From(tenantId));
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
        var customer = await dbContext.Customers.SingleAsync(c => c.Id == CustomerId.From(customerId), cancellationToken);
        customer.LoyaltyPoints.ShouldBe(1);
    }

    [Fact]
    public async Task Duplicate_Earned_Ledger_Entry_For_The_Same_Appointment_Should_Be_Rejected_By_The_Unique_Index()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (customerId, _, appointmentId, tenantId) = await CreateAndCompleteAppointmentAsync(client, accessToken, "cliente@example.com", cancellationToken);

        await PollAsync(
            tenantId,
            dbContext => dbContext.LoyaltyPointsLedgerEntries.SingleOrDefaultAsync(e => e.AppointmentId == appointmentId, cancellationToken),
            cancellationToken);

        // Mesma protecao que torna o consumidor idempotente sob redelivery do
        // RabbitMQ (ver FinanceiroTests.Duplicate_SourceAppointmentId...): o
        // indice unico parcial (tenant_id, appointment_id) WHERE kind = 'Earned'
        // barra no banco mesmo que o codigo esquecesse de checar antes de inserir.
        await using var scope = fixture.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.SetTenant(TenantId.From(tenantId));
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();

        var duplicate = LoyaltyPointsLedgerEntry.RecordEarned(
            TenantId.From(tenantId), CustomerId.From(customerId), 1, appointmentId, DateTimeOffset.UtcNow).Value;
        dbContext.LoyaltyPointsLedgerEntries.Add(duplicate);

        await Should.ThrowAsync<DbUpdateException>(() => dbContext.SaveChangesAsync(cancellationToken));
    }

    [Fact]
    public async Task Owner_Can_Redeem_A_Loyalty_Reward_When_Customer_Has_Enough_Points()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        // Limiar baixo (1 visita) para nao precisar completar 10 agendamentos no teste.
        (await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/loyalty-settings",
            new { loyaltyProgramEnabled = true, loyaltyVisitsForReward = 1, loyaltyRewardDescription = "Corte gratis" },
            cancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (customerId, _, appointmentId, tenantId) = await CreateAndCompleteAppointmentAsync(client, accessToken, "cliente@example.com", cancellationToken);
        await PollAsync(
            tenantId,
            dbContext => dbContext.LoyaltyPointsLedgerEntries.SingleOrDefaultAsync(e => e.AppointmentId == appointmentId, cancellationToken),
            cancellationToken);

        var redeemResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, $"/api/customers/{customerId}/redeem-loyalty-reward", new { }, cancellationToken);
        redeemResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var scope = fixture.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.SetTenant(TenantId.From(tenantId));
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
        var customer = await dbContext.Customers.SingleAsync(c => c.Id == CustomerId.From(customerId), cancellationToken);
        customer.LoyaltyPoints.ShouldBe(0);

        var redeemedEntry = await dbContext.LoyaltyPointsLedgerEntries
            .SingleOrDefaultAsync(e => e.CustomerId == CustomerId.From(customerId) && e.Kind == LoyaltyPointsLedgerEntryKind.Redeemed, cancellationToken);
        redeemedEntry.ShouldNotBeNull();
    }

    [Fact]
    public async Task Redeeming_A_Loyalty_Reward_Without_Enough_Points_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var customerResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/customers", new { fullName = "Cliente Sem Pontos" }, cancellationToken);
        var customerBody = await customerResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var customerId = customerBody.GetProperty("id").GetGuid();

        var redeemResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, $"/api/customers/{customerId}/redeem-loyalty-reward", new { }, cancellationToken);
        redeemResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Public_Can_Look_Up_Loyalty_Points_By_Email()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (_, _, appointmentId, tenantId) = await CreateAndCompleteAppointmentAsync(client, accessToken, "publico@example.com", cancellationToken);

        await PollAsync(
            tenantId,
            dbContext => dbContext.LoyaltyPointsLedgerEntries.SingleOrDefaultAsync(e => e.AppointmentId == appointmentId, cancellationToken),
            cancellationToken);

        var response = await client.GetAsync(
            $"/api/public/tenants/{tenantId}/loyalty?email={Uri.EscapeDataString("publico@example.com")}", cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("loyaltyPoints").GetInt32().ShouldBe(1);
        body.GetProperty("loyaltyVisitsForReward").GetInt32().ShouldBe(10);
    }

    [Fact]
    public async Task Public_Loyalty_Lookup_With_Unknown_Email_Should_Return_Generic_Not_Found()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantId = await GetTenantIdFromTokenAsync(client, accessToken, cancellationToken);

        var response = await client.GetAsync(
            $"/api/public/tenants/{tenantId}/loyalty?email={Uri.EscapeDataString("ninguem@example.com")}", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Loyalty_Points_Are_Isolated_Between_Tenants()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (_, _, appointmentId, tenantAId) = await CreateAndCompleteAppointmentAsync(client, tenantAToken, "cliente-a@example.com", cancellationToken);
        await PollAsync(
            tenantAId,
            dbContext => dbContext.LoyaltyPointsLedgerEntries.SingleOrDefaultAsync(e => e.AppointmentId == appointmentId, cancellationToken),
            cancellationToken);

        var tenantBToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        // Consulta publica pelo e-mail do cliente do tenant A, na rota do tenant B — nao pode achar.
        var tenantBId = await GetTenantIdFromTokenAsync(client, tenantBToken, cancellationToken);
        var response = await client.GetAsync(
            $"/api/public/tenants/{tenantBId}/loyalty?email={Uri.EscapeDataString("cliente-a@example.com")}", cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<(Guid CustomerId, Guid ResourceId, Guid AppointmentId, Guid TenantId)> CreateAndCompleteAppointmentAsync(
        HttpClient client, string accessToken, string customerEmail, CancellationToken cancellationToken)
    {
        var (customerId, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, customerEmail, cancellationToken);
        var appointmentId = await ScheduleConfirmStartCompleteAsync(client, accessToken, customerId, resourceId, serviceId, cancellationToken);
        var tenantId = await GetTenantIdFromTokenAsync(client, accessToken, cancellationToken);

        return (customerId, resourceId, appointmentId, tenantId);
    }

    private async Task<Guid> ScheduleConfirmStartCompleteAsync(
        HttpClient client, string accessToken, Guid customerId, Guid resourceId, Guid serviceId, CancellationToken cancellationToken)
    {
        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc = DateTimeOffset.UtcNow.AddDays(1), notes = (string?)null },
            cancellationToken);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var appointmentId = createBody.GetProperty("id").GetGuid();

        (await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}/confirm", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}/start", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}/complete", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // O outbox de Scheduling so drena pro RabbitMQ no proximo tick do job
        // recorrente (a cada minuto) — forcar o drain aqui evita o teste ter
        // que esperar ate 60s de verdade pelo consumidor de fidelidade.
        await ForceDrainSchedulingOutboxAsync(cancellationToken);

        return appointmentId;
    }

    private async Task ForceDrainSchedulingOutboxAsync(CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var outboxProcessor = scope.ServiceProvider.GetRequiredService<OutboxProcessor<SchedulingDbContext>>();
        await outboxProcessor.ProcessPendingMessagesAsync(cancellationToken);
    }

    /// <summary>Poll com retry curto: LoyaltyIntegrationEventConsumer processa a mensagem de forma assincrona apos o drain do outbox.</summary>
    private async Task<T?> PollAsync<T>(
        Guid tenantId, Func<CustomersDbContext, Task<T?>> query, CancellationToken cancellationToken)
        where T : class
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.SetTenant(TenantId.From(tenantId));
            var dbContext = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();

            var result = await query(dbContext);
            if (result is not null)
            {
                return result;
            }

            await Task.Delay(250, cancellationToken);
        }

        return null;
    }

    private static async Task<Guid> GetTenantIdFromTokenAsync(HttpClient client, string accessToken, CancellationToken cancellationToken)
    {
        var payloadSegment = accessToken.Split('.')[1];
        var padded = payloadSegment.PadRight(payloadSegment.Length + (4 - payloadSegment.Length % 4) % 4, '=');
        var payloadJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/')));
        var payload = JsonDocument.Parse(payloadJson).RootElement;

        _ = cancellationToken;
        return payload.GetProperty("tenant_id").GetGuid();
    }

    private static async Task<(Guid CustomerId, Guid ResourceId, Guid ServiceId)> CreateBookingPrerequisitesAsync(
        HttpClient client, string accessToken, string customerEmail, CancellationToken cancellationToken)
    {
        var customerResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/customers", new { fullName = "Cliente de Teste", email = customerEmail }, cancellationToken);
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
