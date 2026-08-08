using Agendio.Infrastructure.Multitenancy;
using Agendio.Infrastructure.Persistence;
using Agendio.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Agendio.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Usada exclusivamente pela ferramenta `dotnet ef` — nunca pelo container de DI
/// em runtime. Conecta como "PostgresAdmin" (role dona do schema). O
/// NullTenantContext e um stub inofensivo aqui: scaffolding de migration nao
/// executa nenhuma query filtrada por tenant, so compara o modelo C# com o
/// historico de migrations.
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfiguration.Build();
        var connectionString = configuration.GetConnectionString("PostgresAdmin")
            ?? throw new InvalidOperationException("Connection string 'PostgresAdmin' nao configurada para design-time.");

        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        var encryptionOptions = configuration.GetSection(ColumnEncryptionOptions.SectionName).Get<ColumnEncryptionOptions>()
            ?? throw new InvalidOperationException("Secao 'ColumnEncryption' nao configurada para design-time.");
        var encryptionService = new AesGcmEncryptionService(Options.Create(encryptionOptions));

        return new IdentityDbContext(optionsBuilder.Options, new NullTenantContext(), encryptionService);
    }
}
