using Agendio.Infrastructure.Persistence;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Tenancy.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Infrastructure.Persistence;

/// <summary>
/// DbContext PROPRIO do modulo Tenancy — schema "tenancy" isolado. Nenhum outro
/// modulo tem referencia a esta classe; leitura sincrona de outro modulo passa
/// por ITenantLookupService/IUnitLookupService (Agendio.Modules.Tenancy.Contracts).
/// </summary>
public sealed class TenancyDbContext(
    DbContextOptions<TenancyDbContext> options, ITenantContext tenantContext, IEncryptionService encryptionService)
    : AgendioDbContextBase(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;
    private readonly IEncryptionService _encryptionService = encryptionService;

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Unit> Units => Set<Unit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("tenancy");

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenancyDbContext).Assembly);

        // Tenant deliberadamente NAO tem filtro de tenant — e o proprio tenant.
        modelBuilder.Entity<Unit>().HasQueryFilter(u => u.TenantId == CurrentTenantId() && !u.IsDeleted);

        // IEntityTypeConfiguration e instanciado sem parametro por
        // ApplyConfigurationsFromAssembly — o conversor criptografado (que
        // precisa de IEncryptionService) so pode entrar aqui (mesmo padrao de
        // CustomersDbContext para Cpf/HealthNotes).
        modelBuilder.Entity<Tenant>().Property(t => t.WhatsAppAccessToken).HasConversion(new EncryptedStringConverter(_encryptionService));
    }

    private TenantId CurrentTenantId() => _tenantContext.HasTenant ? _tenantContext.TenantId : TenantId.Empty;
}
