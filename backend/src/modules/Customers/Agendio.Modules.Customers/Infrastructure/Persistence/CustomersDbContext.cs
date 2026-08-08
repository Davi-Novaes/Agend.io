using Agendio.Infrastructure.Persistence;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Customers.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Customers.Infrastructure.Persistence;

/// <summary>DbContext PROPRIO do modulo Customers — schema "customers" isolado.</summary>
public sealed class CustomersDbContext(
    DbContextOptions<CustomersDbContext> options, ITenantContext tenantContext, IEncryptionService encryptionService)
    : AgendioDbContextBase(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;
    private readonly IEncryptionService _encryptionService = encryptionService;

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("customers");

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomersDbContext).Assembly);

        modelBuilder.Entity<Customer>().HasQueryFilter(c => c.TenantId == CurrentTenantId() && !c.IsDeleted);

        // IEntityTypeConfiguration e instanciado sem parametro por
        // ApplyConfigurationsFromAssembly — o conversor criptografado (que
        // precisa de IEncryptionService) so pode entrar aqui, depois que o
        // resto do mapeamento de Customer ja rodou.
        // Sem HasMaxLength aqui de proposito: para uma coluna com value
        // converter, o limite se aplicaria ao valor JA CONVERTIDO (base64 de
        // nonce+tag+ciphertext, maior que o texto original) — calcular esse
        // overhead certinho nao vale o risco de truncar um CPF/nota de saude
        // por engano. FluentValidation ja limita o texto plano na entrada
        // (CreateCustomerCommandValidator); a coluna fica "text" sem limite.
        var encryptedConverter = new EncryptedStringConverter(_encryptionService);
        modelBuilder.Entity<Customer>().Property(c => c.Cpf).HasConversion(encryptedConverter);
        modelBuilder.Entity<Customer>().Property(c => c.HealthNotes).HasConversion(encryptedConverter);
    }

    private TenantId CurrentTenantId() => _tenantContext.HasTenant ? _tenantContext.TenantId : TenantId.Empty;
}
