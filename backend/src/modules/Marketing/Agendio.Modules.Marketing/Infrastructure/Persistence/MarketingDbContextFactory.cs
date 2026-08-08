using Agendio.Infrastructure.Multitenancy;
using Agendio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Agendio.Modules.Marketing.Infrastructure.Persistence;

/// <summary>Usada exclusivamente pela ferramenta `dotnet ef` — nunca pelo container de DI em runtime.</summary>
public sealed class MarketingDbContextFactory : IDesignTimeDbContextFactory<MarketingDbContext>
{
    public MarketingDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfiguration.Build();
        var connectionString = configuration.GetConnectionString("PostgresAdmin")
            ?? throw new InvalidOperationException("Connection string 'PostgresAdmin' nao configurada para design-time.");

        var optionsBuilder = new DbContextOptionsBuilder<MarketingDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new MarketingDbContext(optionsBuilder.Options, new NullTenantContext());
    }
}
