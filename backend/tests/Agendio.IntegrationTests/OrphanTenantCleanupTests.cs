using System.Net.Http.Json;
using System.Text.Json;
using Agendio.Infrastructure.Messaging;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Domain;
using Agendio.Modules.Tenancy.Infrastructure.Jobs;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.IntegrationTests;

/// <summary>
/// Cobre P1-1 (docs/AUTH_BILLING_SECURITY_AUDIT.md): squatting de slug por
/// abandono do wizard de cadastro. Dois pedaços: o consumidor de integration
/// event (TenancyIntegrationEventConsumer) que marca FirstUserRegisteredAtUtc
/// quando o registro de verdade acontece, e o job diario que libera o slug de
/// quem nunca registrou ninguem.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class OrphanTenantCleanupTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task Registering_A_User_Should_Eventually_Mark_The_Tenant_As_No_Longer_Abandoned()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantId = await CreateTenantAsync(client, cancellationToken);

        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { tenantId, email = $"dono-{Guid.NewGuid():N}@example.com", password = "SenhaForte123!", fullName = "Dono", phone = "+5511999999999", cpfCnpj = "12345678909", termsAccepted = true },
            cancellationToken);
        registerResponse.EnsureSuccessStatusCode();

        // O outbox do Identity so drena pro RabbitMQ 1x/minuto por padrao
        // (Cron.Minutely(), ver ScheduleOutboxProcessing) — forca a publicacao
        // aqui em vez de esperar o cron, mesmo padrao de
        // PlatformTests.ForceDrainTenancyOutboxAsync.
        await ForceDrainIdentityOutboxAsync(cancellationToken);

        // O consumidor roda em background (RabbitMQ real, ver TenancyIntegrationEventConsumer)
        // — mesmo padrao de poll de PlatformTests.WaitForSubscriptionAsync.
        var markedAtUtc = await PollForFirstUserRegisteredAsync(tenantId, cancellationToken);

        markedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task Cleanup_Job_Should_Release_The_Slug_Of_A_Tenant_Abandoned_For_More_Than_48_Hours()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var tenantId = await CreateTenantAsync(client, cancellationToken);
        var originalSlug = await BackdateTenantCreationAsync(tenantId, TimeSpan.FromHours(49), cancellationToken);

        await RunCleanupJobAsync(cancellationToken);

        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
        var tenant = await dbContext.Tenants.AsNoTracking().SingleAsync(t => t.Id == TenantId.From(tenantId), cancellationToken);

        tenant.IsActive.ShouldBeFalse();
        tenant.Slug.Value.ShouldNotBe(originalSlug);

        // O slug original volta a ficar disponivel para um cadastro novo.
        var slugAvailableAgain = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = "Outro Estabelecimento",
            slug = originalSlug,
            businessType = "Other",
            timeZoneId = "America/Sao_Paulo",
        }, cancellationToken);
        slugAvailableAgain.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Cleanup_Job_Should_Not_Touch_A_Tenant_Created_Less_Than_48_Hours_Ago()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var tenantId = await CreateTenantAsync(client, cancellationToken);
        // Recem-criado (poucos segundos) — dentro da janela de tolerancia.

        await RunCleanupJobAsync(cancellationToken);

        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
        var tenant = await dbContext.Tenants.AsNoTracking().SingleAsync(t => t.Id == TenantId.From(tenantId), cancellationToken);

        tenant.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Cleanup_Job_Should_Not_Touch_An_Abandoned_Looking_Tenant_That_Already_Has_A_Registered_User()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var tenantId = await CreateTenantAsync(client, cancellationToken);

        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { tenantId, email = $"dono-{Guid.NewGuid():N}@example.com", password = "SenhaForte123!", fullName = "Dono", phone = "+5511999999999", cpfCnpj = "12345678909", termsAccepted = true },
            cancellationToken);
        registerResponse.EnsureSuccessStatusCode();
        await ForceDrainIdentityOutboxAsync(cancellationToken);
        (await PollForFirstUserRegisteredAsync(tenantId, cancellationToken)).ShouldNotBeNull();

        var originalSlug = await BackdateTenantCreationAsync(tenantId, TimeSpan.FromHours(49), cancellationToken);

        await RunCleanupJobAsync(cancellationToken);

        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
        var tenant = await dbContext.Tenants.AsNoTracking().SingleAsync(t => t.Id == TenantId.From(tenantId), cancellationToken);

        tenant.IsActive.ShouldBeTrue();
        tenant.Slug.Value.ShouldBe(originalSlug);
    }

    private static async Task<Guid> CreateTenantAsync(HttpClient client, CancellationToken cancellationToken)
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
        return tenantBody.GetProperty("id").GetGuid();
    }

    private async Task ForceDrainIdentityOutboxAsync(CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var outboxProcessor = scope.ServiceProvider.GetRequiredService<OutboxProcessor<IdentityDbContext>>();
        await outboxProcessor.ProcessPendingMessagesAsync(cancellationToken);
    }

    private async Task RunCleanupJobAsync(CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<OrphanTenantCleanupJob>();
        await job.RunAsync(cancellationToken);
    }

    /// <summary>Escreve CreatedAtUtc direto (setter privado de proposito, ver Domain/Tenant.cs) — simula um tenant velho sem esperar 49h de verdade.</summary>
    private async Task<string> BackdateTenantCreationAsync(Guid tenantId, TimeSpan age, CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();

        var tenant = await dbContext.Tenants.SingleAsync(t => t.Id == TenantId.From(tenantId), cancellationToken);
        dbContext.Entry(tenant).Property(nameof(Tenant.CreatedAtUtc)).CurrentValue = DateTimeOffset.UtcNow.Subtract(age);
        await dbContext.SaveChangesAsync(cancellationToken);

        return tenant.Slug.Value;
    }

    private async Task<DateTimeOffset?> PollForFirstUserRegisteredAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
            var tenant = await dbContext.Tenants.AsNoTracking()
                .SingleOrDefaultAsync(t => t.Id == TenantId.From(tenantId), cancellationToken);

            if (tenant?.FirstUserRegisteredAtUtc is not null)
            {
                return tenant.FirstUserRegisteredAtUtc;
            }

            await Task.Delay(250, cancellationToken);
        }

        return null;
    }
}
