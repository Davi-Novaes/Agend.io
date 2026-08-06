using Agendio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Agendio.Modules.Tenancy.Infrastructure.Persistence;

/// <summary>
/// Usada exclusivamente pela ferramenta `dotnet ef` (migrations add/database
/// update) — nunca pelo container de DI em runtime. Conecta como
/// "PostgresAdmin" (role dona do schema); a role de runtime da aplicacao nao
/// tem privilegio de DDL para criar/alterar tabela.
/// </summary>
public sealed class TenancyDbContextFactory : IDesignTimeDbContextFactory<TenancyDbContext>
{
    public TenancyDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfiguration.Build();
        var connectionString = configuration.GetConnectionString("PostgresAdmin")
            ?? throw new InvalidOperationException("Connection string 'PostgresAdmin' nao configurada para design-time.");

        var optionsBuilder = new DbContextOptionsBuilder<TenancyDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new TenancyDbContext(optionsBuilder.Options);
    }
}
