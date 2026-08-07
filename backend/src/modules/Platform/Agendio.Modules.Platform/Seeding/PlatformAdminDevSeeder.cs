using Agendio.Infrastructure.Security;
using Agendio.Modules.Platform.Domain;
using Agendio.Modules.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.Modules.Platform.Seeding;

/// <summary>
/// Garante que existe pelo menos um Super Admin em ambiente de Development, lido
/// da secao "PlatformAdmin" (Email/Password) do appsettings.Development.json —
/// nunca commitada com valor de producao. So roda se a secao existir E o admin
/// ainda nao existir (idempotente entre reinicios). Producao usa um processo de
/// provisionamento fora de escopo do MVP (ver comentario em Program.cs).
/// </summary>
public static class PlatformAdminDevSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var email = configuration["PlatformAdmin:Email"];
        var password = configuration["PlatformAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var alreadyExists = await dbContext.PlatformAdmins.AnyAsync(a => a.Email == normalizedEmail);
        if (alreadyExists)
        {
            return;
        }

        var adminResult = PlatformAdmin.Create(email, "Super Admin", passwordHasher.Hash(password));
        if (adminResult.IsFailure)
        {
            return;
        }

        dbContext.PlatformAdmins.Add(adminResult.Value);
        await dbContext.SaveChangesAsync();
    }
}
