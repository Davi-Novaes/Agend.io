using Agendio.Infrastructure.Persistence;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Platform.Infrastructure.Persistence;

/// <summary>
/// DbContext PROPRIO do modulo Platform — schema "platform" isolado. Sem
/// entidade ITenantOwned nenhuma (PlatformAdmin fica fora de qualquer tenant),
/// entao nao ha Global Query Filter aqui — ao contrario de todo outro DbContext
/// de modulo, este nem precisa de ITenantContext no construtor.
/// </summary>
public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options, IEncryptionService encryptionService) : AgendioDbContextBase(options)
{
    private readonly IEncryptionService _encryptionService = encryptionService;

    public DbSet<PlatformAdmin> PlatformAdmins => Set<PlatformAdmin>();

    public DbSet<SecurityAuditLogEntry> SecurityAuditLog => Set<SecurityAuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("platform");

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);
        modelBuilder.ConfigureSecurityAuditLog();

        // Ver comentario equivalente em IdentityDbContext: o conversor
        // criptografado depende de IEncryptionService, so pode ser aplicado
        // aqui, nunca dentro de PlatformAdminConfiguration (instanciada sem parametro).
        modelBuilder.Entity<PlatformAdmin>().Property(a => a.MfaSecretEncrypted).HasConversion(new EncryptedStringConverter(_encryptionService));
    }
}
