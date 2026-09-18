using Agendio.Infrastructure.Multitenancy;
using Agendio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Agendio.Modules.Assistant.Infrastructure.Persistence;

/// <summary>Usada exclusivamente pela ferramenta `dotnet ef` — nunca pelo container de DI em runtime.</summary>
public sealed class AssistantDbContextFactory : IDesignTimeDbContextFactory<AssistantDbContext>
{
    public AssistantDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfiguration.Build();
        var connectionString = configuration.GetConnectionString("PostgresAdmin")
            ?? throw new InvalidOperationException("Connection string 'PostgresAdmin' nao configurada para design-time.");

        var optionsBuilder = new DbContextOptionsBuilder<AssistantDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new AssistantDbContext(optionsBuilder.Options, new NullTenantContext());
    }
}
