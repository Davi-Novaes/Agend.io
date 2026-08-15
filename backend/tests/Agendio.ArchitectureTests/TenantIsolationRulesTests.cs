using Agendio.Infrastructure.Multitenancy;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Billing.Domain;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.Modules.Catalog.Domain;
using Agendio.Modules.Catalog.Infrastructure.Persistence;
using Agendio.Modules.Customers.Domain;
using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.Modules.Estoque.Domain;
using Agendio.Modules.Estoque.Infrastructure.Persistence;
using Agendio.Modules.Financeiro.Domain;
using Agendio.Modules.Financeiro.Infrastructure.Persistence;
using Agendio.Modules.Identity.Domain;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.Modules.Resources.Domain;
using Agendio.Modules.Resources.Infrastructure.Persistence;
using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Domain;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Agendio.ArchitectureTests;

/// <summary>
/// Garante a regra "toda entidade ITenantOwned tem Global Query Filter" (ver
/// ITenantOwned.cs). Nao precisa de banco de verdade: construir o Model do EF
/// Core so exige uma connection string sintaticamente valida, nunca abre conexao.
///
/// Cada teste afirma a CONTAGEM esperada de entidades ITenantOwned do modulo,
/// nao so "maior que zero" — assim tanto esquecer de marcar uma entidade nova
/// como ITenantOwned quanto remover uma sem querer quebram o teste.
/// </summary>
public class TenantIsolationRulesTests
{
    [Fact]
    public void Identity_Module_Should_Have_Exactly_Four_TenantOwned_Entities_All_With_A_Query_Filter()
    {
        using var dbContext = CreateIdentityDbContext();

        var tenantOwnedEntityTypes = dbContext.Model.GetEntityTypes()
            .Where(entityType => typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            .ToList();

        tenantOwnedEntityTypes.Select(e => e.ClrType).ShouldBe(
            [typeof(User), typeof(RefreshToken), typeof(TeamInvitation), typeof(MfaRecoveryCode)], ignoreOrder: true);

        foreach (var entityType in tenantOwnedEntityTypes)
        {
            entityType.GetDeclaredQueryFilters().ShouldNotBeEmpty(
                $"{entityType.ClrType.Name} implementa ITenantOwned mas nao tem Global Query Filter configurado no DbContext.");
        }
    }

    [Fact]
    public void Customers_Module_Should_Have_Exactly_Two_TenantOwned_Entities_All_With_A_Query_Filter()
    {
        using var dbContext = CreateCustomersDbContext();

        var tenantOwnedEntityTypes = dbContext.Model.GetEntityTypes()
            .Where(entityType => typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            .ToList();

        tenantOwnedEntityTypes.Select(e => e.ClrType).ShouldBe([typeof(Customer), typeof(LoyaltyPointsLedgerEntry)], ignoreOrder: true);

        foreach (var entityType in tenantOwnedEntityTypes)
        {
            entityType.GetDeclaredQueryFilters().ShouldNotBeEmpty(
                $"{entityType.ClrType.Name} implementa ITenantOwned mas nao tem Global Query Filter configurado no DbContext.");
        }
    }

    [Fact]
    public void Catalog_Module_Should_Have_Exactly_One_TenantOwned_Entity_With_A_Query_Filter()
    {
        using var dbContext = CreateCatalogDbContext();

        var tenantOwnedEntityTypes = dbContext.Model.GetEntityTypes()
            .Where(entityType => typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            .ToList();

        tenantOwnedEntityTypes.Select(e => e.ClrType).ShouldBe([typeof(Service)]);

        foreach (var entityType in tenantOwnedEntityTypes)
        {
            entityType.GetDeclaredQueryFilters().ShouldNotBeEmpty(
                $"{entityType.ClrType.Name} implementa ITenantOwned mas nao tem Global Query Filter configurado no DbContext.");
        }
    }

    [Fact]
    public void Resources_Module_Should_Have_Exactly_Two_TenantOwned_Entities_All_With_A_Query_Filter()
    {
        using var dbContext = CreateResourcesDbContext();

        var tenantOwnedEntityTypes = dbContext.Model.GetEntityTypes()
            .Where(entityType => typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            .ToList();

        tenantOwnedEntityTypes.Select(e => e.ClrType).ShouldBe([typeof(Resource), typeof(TimeOff)], ignoreOrder: true);

        foreach (var entityType in tenantOwnedEntityTypes)
        {
            entityType.GetDeclaredQueryFilters().ShouldNotBeEmpty(
                $"{entityType.ClrType.Name} implementa ITenantOwned mas nao tem Global Query Filter configurado no DbContext.");
        }
    }

    [Fact]
    public void Scheduling_Module_Should_Have_Exactly_Four_TenantOwned_Entities_With_A_Query_Filter()
    {
        using var dbContext = CreateSchedulingDbContext();

        var tenantOwnedEntityTypes = dbContext.Model.GetEntityTypes()
            .Where(entityType => typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            .ToList();

        tenantOwnedEntityTypes.Select(e => e.ClrType).ShouldBe(
            [typeof(Appointment), typeof(NotificationLogEntry), typeof(Review), typeof(WaitlistEntry)], ignoreOrder: true);

        foreach (var entityType in tenantOwnedEntityTypes)
        {
            entityType.GetDeclaredQueryFilters().ShouldNotBeEmpty(
                $"{entityType.ClrType.Name} implementa ITenantOwned mas nao tem Global Query Filter configurado no DbContext.");
        }
    }

    [Fact]
    public void Tenancy_Module_Should_Have_Exactly_One_TenantOwned_Entity_All_With_A_Query_Filter()
    {
        // Tenant E o tenant — nao pertence a um. Se algum dia Tenant passar a
        // implementar ITenantOwned, algo mudou fundamentalmente no modelo e
        // merece revisao explicita, nao passar silenciosamente.
        typeof(ITenantOwned).IsAssignableFrom(typeof(Tenant)).ShouldBeFalse();

        using var dbContext = CreateTenancyDbContext();

        var tenantOwnedEntityTypes = dbContext.Model.GetEntityTypes()
            .Where(entityType => typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            .ToList();

        tenantOwnedEntityTypes.Select(e => e.ClrType).ShouldBe([typeof(Unit)]);

        foreach (var entityType in tenantOwnedEntityTypes)
        {
            entityType.GetDeclaredQueryFilters().ShouldNotBeEmpty(
                $"{entityType.ClrType.Name} implementa ITenantOwned mas nao tem Global Query Filter configurado no DbContext.");
        }
    }

    [Fact]
    public void Billing_Module_Should_Deliberately_Have_Zero_TenantOwned_Entities()
    {
        // Subscription/Payment tem coluna tenant_id normal, mas nao sao
        // ITenantOwned e nao tem RLS de proposito — agendio_owner e agendio_app
        // sao ambos NOBYPASSRLS, entao RLS aqui deixaria o painel Super Admin e
        // o job de conciliacao cegos para todo tenant. Ver comentario em
        // Subscription.cs. Se algum dia isso mudar, merece revisao explicita.
        typeof(ITenantOwned).IsAssignableFrom(typeof(Subscription)).ShouldBeFalse();
        typeof(ITenantOwned).IsAssignableFrom(typeof(Payment)).ShouldBeFalse();
        typeof(ITenantOwned).IsAssignableFrom(typeof(Plan)).ShouldBeFalse();

        using var dbContext = CreateBillingDbContext();
        var tenantOwnedEntityTypes = dbContext.Model.GetEntityTypes()
            .Where(entityType => typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType));

        tenantOwnedEntityTypes.ShouldBeEmpty();
    }

    [Fact]
    public void Financeiro_Module_Should_Have_Exactly_Three_TenantOwned_Entities_All_With_A_Query_Filter()
    {
        using var dbContext = CreateFinanceiroDbContext();

        var tenantOwnedEntityTypes = dbContext.Model.GetEntityTypes()
            .Where(entityType => typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            .ToList();

        tenantOwnedEntityTypes.Select(e => e.ClrType).ShouldBe(
            [typeof(AccountReceivable), typeof(AccountPayable), typeof(CommissionRule)], ignoreOrder: true);

        foreach (var entityType in tenantOwnedEntityTypes)
        {
            entityType.GetDeclaredQueryFilters().ShouldNotBeEmpty(
                $"{entityType.ClrType.Name} implementa ITenantOwned mas nao tem Global Query Filter configurado no DbContext.");
        }
    }

    [Fact]
    public void Estoque_Module_Should_Have_Exactly_Two_TenantOwned_Entities_All_With_A_Query_Filter()
    {
        using var dbContext = CreateEstoqueDbContext();

        var tenantOwnedEntityTypes = dbContext.Model.GetEntityTypes()
            .Where(entityType => typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            .ToList();

        tenantOwnedEntityTypes.Select(e => e.ClrType).ShouldBe([typeof(Product), typeof(StockMovement)], ignoreOrder: true);

        foreach (var entityType in tenantOwnedEntityTypes)
        {
            entityType.GetDeclaredQueryFilters().ShouldNotBeEmpty(
                $"{entityType.ClrType.Name} implementa ITenantOwned mas nao tem Global Query Filter configurado no DbContext.");
        }
    }

    private static IdentityDbContext CreateIdentityDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=architecture_tests_only")
            .UseSnakeCaseNamingConvention();

        return new IdentityDbContext(optionsBuilder.Options, new NullTenantContext(), CreateEncryptionService());
    }

    private static TenancyDbContext CreateTenancyDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseNpgsql("Host=localhost;Database=architecture_tests_only")
            .UseSnakeCaseNamingConvention();

        return new TenancyDbContext(optionsBuilder.Options, new NullTenantContext(), CreateEncryptionService());
    }

    private static CustomersDbContext CreateCustomersDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseNpgsql("Host=localhost;Database=architecture_tests_only")
            .UseSnakeCaseNamingConvention();

        return new CustomersDbContext(optionsBuilder.Options, new NullTenantContext(), CreateEncryptionService());
    }

    // So o Model precisa ser construido aqui (nunca abre conexao de verdade),
    // mas o EncryptedStringConverter de Customer.Cpf/HealthNotes exige um
    // IEncryptionService valido no construtor do DbContext — qualquer chave de
    // 32 bytes serve, nada e efetivamente criptografado/descriptografado neste teste.
    private static AesGcmEncryptionService CreateEncryptionService() =>
        new(Options.Create(new ColumnEncryptionOptions { Key = "aW50ZWdyYXRpb24tdGVzdC1jb2wta2V5LTMyYnl0ZXM=" }));

    private static CatalogDbContext CreateCatalogDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql("Host=localhost;Database=architecture_tests_only")
            .UseSnakeCaseNamingConvention();

        return new CatalogDbContext(optionsBuilder.Options, new NullTenantContext());
    }

    private static ResourcesDbContext CreateResourcesDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ResourcesDbContext>()
            .UseNpgsql("Host=localhost;Database=architecture_tests_only")
            .UseSnakeCaseNamingConvention();

        return new ResourcesDbContext(optionsBuilder.Options, new NullTenantContext());
    }

    private static SchedulingDbContext CreateSchedulingDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<SchedulingDbContext>()
            .UseNpgsql("Host=localhost;Database=architecture_tests_only")
            .UseSnakeCaseNamingConvention();

        return new SchedulingDbContext(optionsBuilder.Options, new NullTenantContext());
    }

    private static FinanceiroDbContext CreateFinanceiroDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<FinanceiroDbContext>()
            .UseNpgsql("Host=localhost;Database=architecture_tests_only")
            .UseSnakeCaseNamingConvention();

        return new FinanceiroDbContext(optionsBuilder.Options, new NullTenantContext());
    }

    private static EstoqueDbContext CreateEstoqueDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<EstoqueDbContext>()
            .UseNpgsql("Host=localhost;Database=architecture_tests_only")
            .UseSnakeCaseNamingConvention();

        return new EstoqueDbContext(optionsBuilder.Options, new NullTenantContext());
    }

    private static BillingDbContext CreateBillingDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql("Host=localhost;Database=architecture_tests_only")
            .UseSnakeCaseNamingConvention();

        return new BillingDbContext(optionsBuilder.Options);
    }
}
