using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.IntegrationTests;

/// <summary>
/// Desde a Fase 23, login exige e-mail confirmado — todo teste que registra um
/// usuario e loga em seguida (a maioria da suite) precisa passar por aqui antes
/// do login, senao recebe Auth.EmailNotConfirmed. Escreve EmailConfirmedAt
/// direto via EF Core Entry(...).Property(...) (setter privado em User de
/// proposito, ver Domain/User.cs) em vez de resolver o token de verdade — os
/// testes DEDICADOS de confirmacao de e-mail (EmailConfirmationTests.cs) sao os
/// que exercitam o fluxo real (MailHog + token), esta aqui e so infraestrutura
/// de setup para testes que nao sao sobre confirmacao de e-mail.
/// </summary>
public static class EmailConfirmationTestHelpers
{
    public static async Task ConfirmEmailDirectlyAsync(
        this IntegrationTestFixture fixture, Guid tenantId, string email, CancellationToken cancellationToken)
    {
        using var scope = fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        tenantContext.SetTenant(TenantId.From(tenantId));

        var emailValue = Email.Create(email).Value;
        var user = await dbContext.Users.SingleAsync(u => u.Email == emailValue, cancellationToken);

        dbContext.Entry(user).Property(nameof(Agendio.Modules.Identity.Domain.User.EmailConfirmedAt)).CurrentValue = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
