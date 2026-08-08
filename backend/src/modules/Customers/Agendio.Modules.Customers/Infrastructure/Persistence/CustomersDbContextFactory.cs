using Agendio.Infrastructure.Multitenancy;
using Agendio.Infrastructure.Persistence;
using Agendio.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Agendio.Modules.Customers.Infrastructure.Persistence;

/// <summary>Usada exclusivamente pela ferramenta `dotnet ef` — nunca pelo container de DI em runtime.</summary>
public sealed class CustomersDbContextFactory : IDesignTimeDbContextFactory<CustomersDbContext>
{
    public CustomersDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfiguration.Build();
        var connectionString = configuration.GetConnectionString("PostgresAdmin")
            ?? throw new InvalidOperationException("Connection string 'PostgresAdmin' nao configurada para design-time.");

        var optionsBuilder = new DbContextOptionsBuilder<CustomersDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        var encryptionOptions = configuration.GetSection(ColumnEncryptionOptions.SectionName).Get<ColumnEncryptionOptions>()
            ?? throw new InvalidOperationException("Secao 'ColumnEncryption' nao configurada para design-time.");
        var encryptionService = new AesGcmEncryptionService(Options.Create(encryptionOptions));

        return new CustomersDbContext(optionsBuilder.Options, new NullTenantContext(), encryptionService);
    }
}
