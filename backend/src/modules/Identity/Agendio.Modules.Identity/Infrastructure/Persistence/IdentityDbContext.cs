using Agendio.Infrastructure.Persistence;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// DbContext PROPRIO do modulo Identity — schema "identity" isolado.
///
/// O Global Query Filter referencia _tenantContext (o SERVICO, nao um valor
/// calculado uma vez) e chama CurrentTenantId() a cada execucao de query. Isso
/// e essencial: ITenantContext.SetTenant() pode ser chamado DEPOIS que este
/// DbContext ja foi resolvido pelo DI (ver ExplicitTenantBehavior e o fluxo de
/// refresh de token), entao um campo calculado no construtor ficaria congelado
/// com o valor de ANTES do tenant ser ancorado. Referenciar o servico e chamar
/// um metodo simples sobre ele e o padrao suportado pelo EF Core para filtros
/// dependentes de estado que muda durante o ciclo de vida do DbContext.
/// </summary>
public sealed class IdentityDbContext(
    DbContextOptions<IdentityDbContext> options, ITenantContext tenantContext, IEncryptionService encryptionService)
    : AgendioDbContextBase(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;
    private readonly IEncryptionService _encryptionService = encryptionService;

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<TeamInvitation> TeamInvitations => Set<TeamInvitation>();

    public DbSet<MfaRecoveryCode> MfaRecoveryCodes => Set<MfaRecoveryCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        // Um unico HasQueryFilter por entidade: chamar de novo SOBRESCREVE (nao
        // combina) o filtro anterior, entao tenant e soft-delete precisam vir
        // juntos na mesma expressao — nunca configurados em dois lugares.
        modelBuilder.Entity<User>().HasQueryFilter(u => u.TenantId == CurrentTenantId() && !u.IsDeleted);
        modelBuilder.Entity<RefreshToken>().HasQueryFilter(rt => rt.TenantId == CurrentTenantId());
        modelBuilder.Entity<TeamInvitation>().HasQueryFilter(i => i.TenantId == CurrentTenantId());
        modelBuilder.Entity<MfaRecoveryCode>().HasQueryFilter(rc => rc.TenantId == CurrentTenantId());

        // Ver comentario equivalente em CustomersDbContext: o conversor
        // criptografado depende de IEncryptionService, entao so pode ser
        // aplicado aqui (depois de ApplyConfigurationsFromAssembly), nunca
        // dentro de UserConfiguration (instanciada sem parametro).
        modelBuilder.Entity<User>().Property(u => u.MfaSecretEncrypted).HasConversion(new EncryptedStringConverter(_encryptionService));
    }

    private TenantId CurrentTenantId() => _tenantContext.HasTenant ? _tenantContext.TenantId : TenantId.Empty;
}
