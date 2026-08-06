using Agendio.Infrastructure.Multitenancy;
using Agendio.Modules.Identity.Domain;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Domain;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;

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
    public void Identity_Module_Should_Have_Exactly_Three_TenantOwned_Entities_All_With_A_Query_Filter()
    {
        using var dbContext = CreateIdentityDbContext();

        var tenantOwnedEntityTypes = dbContext.Model.GetEntityTypes()
            .Where(entityType => typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            .ToList();

        tenantOwnedEntityTypes.Select(e => e.ClrType).ShouldBe(
            [typeof(User), typeof(RefreshToken), typeof(TeamInvitation)], ignoreOrder: true);

        foreach (var entityType in tenantOwnedEntityTypes)
        {
            entityType.GetDeclaredQueryFilters().ShouldNotBeEmpty(
                $"{entityType.ClrType.Name} implementa ITenantOwned mas nao tem Global Query Filter configurado no DbContext.");
        }
    }

    [Fact]
    public void Tenant_Should_Deliberately_Not_Be_TenantOwned()
    {
        // Tenant E o tenant — nao pertence a um. Se algum dia Tenant passar a
        // implementar ITenantOwned, algo mudou fundamentalmente no modelo e
        // merece revisao explicita, nao passar silenciosamente.
        typeof(ITenantOwned).IsAssignableFrom(typeof(Tenant)).ShouldBeFalse();

        using var dbContext = CreateTenancyDbContext();
        var tenantOwnedEntityTypes = dbContext.Model.GetEntityTypes()
            .Where(entityType => typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType));

        tenantOwnedEntityTypes.ShouldBeEmpty();
    }

    private static IdentityDbContext CreateIdentityDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=architecture_tests_only")
            .UseSnakeCaseNamingConvention();

        return new IdentityDbContext(optionsBuilder.Options, new NullTenantContext());
    }

    private static TenancyDbContext CreateTenancyDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseNpgsql("Host=localhost;Database=architecture_tests_only")
            .UseSnakeCaseNamingConvention();

        return new TenancyDbContext(optionsBuilder.Options);
    }
}
