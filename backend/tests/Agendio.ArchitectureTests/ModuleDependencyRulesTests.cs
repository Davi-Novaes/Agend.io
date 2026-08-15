using System.Reflection;
using NetArchTest.Rules;

namespace Agendio.ArchitectureTests;

/// <summary>
/// Verifica as regras de dependencia descritas em CLAUDE.md:
/// 1. Domain nao referencia Application/Infrastructure do proprio modulo.
/// 2. Um modulo nunca referencia outro modulo — so o .Contracts dele.
/// Quebrar qualquer uma destas regras deveria quebrar o build, nao virar bug
/// descoberto em producao.
/// </summary>
public class ModuleDependencyRulesTests
{
    private static readonly Assembly IdentityAssembly = typeof(Agendio.Modules.Identity.Domain.User).Assembly;
    private static readonly Assembly TenancyAssembly = typeof(Agendio.Modules.Tenancy.Domain.Tenant).Assembly;
    private static readonly Assembly CustomersAssembly = typeof(Agendio.Modules.Customers.Domain.Customer).Assembly;
    private static readonly Assembly CatalogAssembly = typeof(Agendio.Modules.Catalog.Domain.Service).Assembly;
    private static readonly Assembly ResourcesAssembly = typeof(Agendio.Modules.Resources.Domain.Resource).Assembly;
    private static readonly Assembly SchedulingAssembly = typeof(Agendio.Modules.Scheduling.Domain.Appointment).Assembly;
    private static readonly Assembly PlatformAssembly = typeof(Agendio.Modules.Platform.Domain.PlatformAdmin).Assembly;
    private static readonly Assembly BillingAssembly = typeof(Agendio.Modules.Billing.Domain.Subscription).Assembly;
    private static readonly Assembly FinanceiroAssembly = typeof(Agendio.Modules.Financeiro.Domain.AccountReceivable).Assembly;
    private static readonly Assembly EstoqueAssembly = typeof(Agendio.Modules.Estoque.Domain.Product).Assembly;
    private static readonly Assembly MarketingAssembly = typeof(Agendio.Modules.Marketing.Domain.Campaign).Assembly;

    [Theory]
    [InlineData("Agendio.Modules.Identity.Domain")]
    [InlineData("Agendio.Modules.Tenancy.Domain")]
    [InlineData("Agendio.Modules.Customers.Domain")]
    [InlineData("Agendio.Modules.Catalog.Domain")]
    [InlineData("Agendio.Modules.Resources.Domain")]
    [InlineData("Agendio.Modules.Scheduling.Domain")]
    [InlineData("Agendio.Modules.Platform.Domain")]
    [InlineData("Agendio.Modules.Billing.Domain")]
    [InlineData("Agendio.Modules.Financeiro.Domain")]
    [InlineData("Agendio.Modules.Estoque.Domain")]
    [InlineData("Agendio.Modules.Marketing.Domain")]
    public void Domain_Should_Not_Depend_On_Application_Or_Infrastructure(string domainNamespace)
    {
        var assembly = domainNamespace switch
        {
            _ when domainNamespace.Contains("Identity") => IdentityAssembly,
            _ when domainNamespace.Contains("Customers") => CustomersAssembly,
            _ when domainNamespace.Contains("Catalog") => CatalogAssembly,
            _ when domainNamespace.Contains("Resources") => ResourcesAssembly,
            _ when domainNamespace.Contains("Scheduling") => SchedulingAssembly,
            _ when domainNamespace.Contains("Platform") => PlatformAssembly,
            _ when domainNamespace.Contains("Billing") => BillingAssembly,
            _ when domainNamespace.Contains("Financeiro") => FinanceiroAssembly,
            _ when domainNamespace.Contains("Estoque") => EstoqueAssembly,
            _ when domainNamespace.Contains("Marketing") => MarketingAssembly,
            _ => TenancyAssembly,
        };
        var moduleRoot = domainNamespace[..domainNamespace.LastIndexOf(".Domain", StringComparison.Ordinal)];

        var result = Types.InAssembly(assembly)
            .That().ResideInNamespace(domainNamespace)
            .ShouldNot().HaveDependencyOnAny([$"{moduleRoot}.Application", $"{moduleRoot}.Infrastructure", $"{moduleRoot}.Endpoints"])
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Domain de {moduleRoot} nao pode depender de Application/Infrastructure/Endpoints. Tipos violando a regra: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Identity_Module_Should_Not_Depend_On_Tenancy_Internals()
    {
        // So ".Contracts" e permitido — listado explicitamente (em vez de checar
        // "nao depende de Agendio.Modules.Tenancy") porque o matching de
        // namespace do NetArchTest e por prefixo: um teste generico tambem
        // acusaria falso-positivo em "Agendio.Modules.Tenancy.Contracts".
        var forbiddenNamespaces = new[]
        {
            "Agendio.Modules.Tenancy.Domain",
            "Agendio.Modules.Tenancy.Application",
            "Agendio.Modules.Tenancy.Infrastructure",
            "Agendio.Modules.Tenancy.Endpoints",
            "Agendio.Modules.Tenancy.DependencyInjection",
        };

        var result = Types.InAssembly(IdentityAssembly)
            .That().ResideInNamespace("Agendio.Modules.Identity")
            .ShouldNot().HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Identity so pode depender de Agendio.Modules.Tenancy.Contracts, nunca do modulo Tenancy inteiro. " +
            "Tipos violando a regra: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Resources_Module_Should_Not_Depend_On_Tenancy_Internals()
    {
        // Resources resolve so a existencia/tenant de uma Unit via
        // IUnitLookupService (validacao de Resource.UnitId) — nunca deveria
        // enxergar Tenancy.Domain diretamente.
        var forbiddenNamespaces = new[]
        {
            "Agendio.Modules.Tenancy.Domain",
            "Agendio.Modules.Tenancy.Application",
            "Agendio.Modules.Tenancy.Infrastructure",
            "Agendio.Modules.Tenancy.Endpoints",
            "Agendio.Modules.Tenancy.DependencyInjection",
        };

        var result = Types.InAssembly(ResourcesAssembly)
            .That().ResideInNamespace("Agendio.Modules.Resources")
            .ShouldNot().HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Resources so pode depender do .Contracts de Tenancy, nunca do modulo inteiro. " +
            "Tipos violando a regra: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Resources_Module_Should_Not_Depend_On_Catalog_Internals()
    {
        // Resources resolve so a existencia/tenant de um Service via
        // IServiceLookupService (validacao de Resource.ServiceIds) — nunca
        // deveria enxergar Catalog.Domain diretamente.
        var forbiddenNamespaces = new[]
        {
            "Agendio.Modules.Catalog.Domain",
            "Agendio.Modules.Catalog.Application",
            "Agendio.Modules.Catalog.Infrastructure",
            "Agendio.Modules.Catalog.Endpoints",
            "Agendio.Modules.Catalog.DependencyInjection",
        };

        var result = Types.InAssembly(ResourcesAssembly)
            .That().ResideInNamespace("Agendio.Modules.Resources")
            .ShouldNot().HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Resources so pode depender do .Contracts de Catalog, nunca do modulo inteiro. " +
            "Tipos violando a regra: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Scheduling_Module_Should_Not_Depend_On_Customers_Catalog_Resources_Or_Tenancy_Internals()
    {
        // Scheduling e o modulo que mais depende de leitura de outros modulos
        // (cliente, servico, recurso, fuso horario do tenant) — exatamente por
        // isso e o teste mais importante para pegar alguem "atalhando" e
        // referenciando o Domain de outro modulo em vez do .Contracts dele.
        var forbiddenNamespaces = new[]
        {
            "Agendio.Modules.Customers.Domain",
            "Agendio.Modules.Customers.Application",
            "Agendio.Modules.Customers.Infrastructure",
            "Agendio.Modules.Customers.Endpoints",
            "Agendio.Modules.Customers.DependencyInjection",
            "Agendio.Modules.Catalog.Domain",
            "Agendio.Modules.Catalog.Application",
            "Agendio.Modules.Catalog.Infrastructure",
            "Agendio.Modules.Catalog.Endpoints",
            "Agendio.Modules.Catalog.DependencyInjection",
            "Agendio.Modules.Resources.Domain",
            "Agendio.Modules.Resources.Application",
            "Agendio.Modules.Resources.Infrastructure",
            "Agendio.Modules.Resources.Endpoints",
            "Agendio.Modules.Resources.DependencyInjection",
            "Agendio.Modules.Tenancy.Domain",
            "Agendio.Modules.Tenancy.Application",
            "Agendio.Modules.Tenancy.Infrastructure",
            "Agendio.Modules.Tenancy.Endpoints",
            "Agendio.Modules.Tenancy.DependencyInjection",
        };

        var result = Types.InAssembly(SchedulingAssembly)
            .That().ResideInNamespace("Agendio.Modules.Scheduling")
            .ShouldNot().HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Scheduling so pode depender dos .Contracts de Customers/Catalog/Resources/Tenancy, nunca dos modulos inteiros. " +
            "Tipos violando a regra: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Platform_Module_Should_Not_Depend_On_Any_Tenant_Facing_Module_Internals()
    {
        // Platform e a autoridade do Super Admin — nao deveria nem PRECISAR
        // conhecer Identity/Customers/Catalog/Resources/Scheduling. So Tenancy
        // e permitido, e so via .Contracts (ITenantAdministrationService).
        var forbiddenNamespaces = new[]
        {
            "Agendio.Modules.Identity.Domain",
            "Agendio.Modules.Identity.Application",
            "Agendio.Modules.Identity.Infrastructure",
            "Agendio.Modules.Identity.Endpoints",
            "Agendio.Modules.Identity.DependencyInjection",
            "Agendio.Modules.Customers.Domain",
            "Agendio.Modules.Customers.Application",
            "Agendio.Modules.Customers.Infrastructure",
            "Agendio.Modules.Customers.Endpoints",
            "Agendio.Modules.Customers.DependencyInjection",
            "Agendio.Modules.Catalog.Domain",
            "Agendio.Modules.Catalog.Application",
            "Agendio.Modules.Catalog.Infrastructure",
            "Agendio.Modules.Catalog.Endpoints",
            "Agendio.Modules.Catalog.DependencyInjection",
            "Agendio.Modules.Resources.Domain",
            "Agendio.Modules.Resources.Application",
            "Agendio.Modules.Resources.Infrastructure",
            "Agendio.Modules.Resources.Endpoints",
            "Agendio.Modules.Resources.DependencyInjection",
            "Agendio.Modules.Scheduling.Domain",
            "Agendio.Modules.Scheduling.Application",
            "Agendio.Modules.Scheduling.Infrastructure",
            "Agendio.Modules.Scheduling.Endpoints",
            "Agendio.Modules.Scheduling.DependencyInjection",
            "Agendio.Modules.Tenancy.Domain",
            "Agendio.Modules.Tenancy.Application",
            "Agendio.Modules.Tenancy.Infrastructure",
            "Agendio.Modules.Tenancy.Endpoints",
            "Agendio.Modules.Tenancy.DependencyInjection",
            "Agendio.Modules.Billing.Domain",
            "Agendio.Modules.Billing.Application",
            "Agendio.Modules.Billing.Infrastructure",
            "Agendio.Modules.Billing.Endpoints",
            "Agendio.Modules.Billing.DependencyInjection",
        };

        var result = Types.InAssembly(PlatformAssembly)
            .That().ResideInNamespace("Agendio.Modules.Platform")
            .ShouldNot().HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Platform so pode depender dos .Contracts de Tenancy/Billing, nunca de nenhum modulo inteiro. " +
            "Tipos violando a regra: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Billing_Module_Should_Not_Depend_On_Any_Tenant_Facing_Module_Internals()
    {
        // Billing so precisa de leitura/escrita cross-modulo em Tenancy
        // (ITenantAdministrationService, no job de conciliacao) — nenhum outro
        // modulo deveria aparecer aqui, nem o proprio Tenancy fora do .Contracts.
        var forbiddenNamespaces = new[]
        {
            "Agendio.Modules.Identity.Domain",
            "Agendio.Modules.Identity.Application",
            "Agendio.Modules.Identity.Infrastructure",
            "Agendio.Modules.Identity.Endpoints",
            "Agendio.Modules.Identity.DependencyInjection",
            "Agendio.Modules.Customers.Domain",
            "Agendio.Modules.Customers.Application",
            "Agendio.Modules.Customers.Infrastructure",
            "Agendio.Modules.Customers.Endpoints",
            "Agendio.Modules.Customers.DependencyInjection",
            "Agendio.Modules.Catalog.Domain",
            "Agendio.Modules.Catalog.Application",
            "Agendio.Modules.Catalog.Infrastructure",
            "Agendio.Modules.Catalog.Endpoints",
            "Agendio.Modules.Catalog.DependencyInjection",
            "Agendio.Modules.Resources.Domain",
            "Agendio.Modules.Resources.Application",
            "Agendio.Modules.Resources.Infrastructure",
            "Agendio.Modules.Resources.Endpoints",
            "Agendio.Modules.Resources.DependencyInjection",
            "Agendio.Modules.Scheduling.Domain",
            "Agendio.Modules.Scheduling.Application",
            "Agendio.Modules.Scheduling.Infrastructure",
            "Agendio.Modules.Scheduling.Endpoints",
            "Agendio.Modules.Scheduling.DependencyInjection",
            "Agendio.Modules.Platform.Domain",
            "Agendio.Modules.Platform.Application",
            "Agendio.Modules.Platform.Infrastructure",
            "Agendio.Modules.Platform.Endpoints",
            "Agendio.Modules.Platform.DependencyInjection",
            "Agendio.Modules.Tenancy.Domain",
            "Agendio.Modules.Tenancy.Application",
            "Agendio.Modules.Tenancy.Infrastructure",
            "Agendio.Modules.Tenancy.Endpoints",
            "Agendio.Modules.Tenancy.DependencyInjection",
        };

        var result = Types.InAssembly(BillingAssembly)
            .That().ResideInNamespace("Agendio.Modules.Billing")
            .ShouldNot().HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Billing so pode depender do .Contracts de Tenancy, nunca de nenhum modulo inteiro. " +
            "Tipos violando a regra: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Financeiro_Module_Should_Not_Depend_On_Resources_Or_Scheduling_Internals()
    {
        // Financeiro reage a AppointmentCompletedDomainEvent (via
        // SchedulingIntegrationEventTypes, so uma constante de string) e resolve
        // nome/tipo de profissional via IResourceLookupService — nunca deveria
        // enxergar Resources.Domain ou Scheduling.Domain diretamente.
        var forbiddenNamespaces = new[]
        {
            "Agendio.Modules.Resources.Domain",
            "Agendio.Modules.Resources.Application",
            "Agendio.Modules.Resources.Infrastructure",
            "Agendio.Modules.Resources.Endpoints",
            "Agendio.Modules.Resources.DependencyInjection",
            "Agendio.Modules.Scheduling.Domain",
            "Agendio.Modules.Scheduling.Application",
            "Agendio.Modules.Scheduling.Infrastructure",
            "Agendio.Modules.Scheduling.Endpoints",
            "Agendio.Modules.Scheduling.DependencyInjection",
        };

        var result = Types.InAssembly(FinanceiroAssembly)
            .That().ResideInNamespace("Agendio.Modules.Financeiro")
            .ShouldNot().HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Financeiro so pode depender dos .Contracts de Resources/Scheduling, nunca dos modulos inteiros. " +
            "Tipos violando a regra: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Marketing_Module_Should_Not_Depend_On_Any_Module_Internals_Except_Customers_Contracts()
    {
        // Marketing so precisa resolver a lista de clientes ativos com e-mail
        // (ICustomerLookupService.ListActiveWithEmailAsync) — nunca deveria
        // enxergar Customers.Domain ou qualquer outro modulo diretamente.
        var forbiddenNamespaces = new[]
        {
            "Agendio.Modules.Customers.Domain",
            "Agendio.Modules.Customers.Application",
            "Agendio.Modules.Customers.Infrastructure",
            "Agendio.Modules.Customers.Endpoints",
            "Agendio.Modules.Customers.DependencyInjection",
            "Agendio.Modules.Identity.Domain",
            "Agendio.Modules.Identity.Application",
            "Agendio.Modules.Identity.Infrastructure",
            "Agendio.Modules.Identity.Endpoints",
            "Agendio.Modules.Identity.DependencyInjection",
            "Agendio.Modules.Catalog.Domain",
            "Agendio.Modules.Catalog.Application",
            "Agendio.Modules.Catalog.Infrastructure",
            "Agendio.Modules.Catalog.Endpoints",
            "Agendio.Modules.Catalog.DependencyInjection",
            "Agendio.Modules.Resources.Domain",
            "Agendio.Modules.Resources.Application",
            "Agendio.Modules.Resources.Infrastructure",
            "Agendio.Modules.Resources.Endpoints",
            "Agendio.Modules.Resources.DependencyInjection",
            "Agendio.Modules.Scheduling.Domain",
            "Agendio.Modules.Scheduling.Application",
            "Agendio.Modules.Scheduling.Infrastructure",
            "Agendio.Modules.Scheduling.Endpoints",
            "Agendio.Modules.Scheduling.DependencyInjection",
            "Agendio.Modules.Tenancy.Domain",
            "Agendio.Modules.Tenancy.Application",
            "Agendio.Modules.Tenancy.Infrastructure",
            "Agendio.Modules.Tenancy.Endpoints",
            "Agendio.Modules.Tenancy.DependencyInjection",
        };

        var result = Types.InAssembly(MarketingAssembly)
            .That().ResideInNamespace("Agendio.Modules.Marketing")
            .ShouldNot().HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Marketing so pode depender do .Contracts de Customers, nunca de nenhum modulo inteiro. " +
            "Tipos violando a regra: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Customers_Module_Should_Not_Depend_On_Scheduling_Internals()
    {
        // Fase 9 (auto-segmentacao): Customers resolve agregados de visita via
        // ICustomerVisitStatsLookupService — nunca deveria enxergar Appointment
        // (Scheduling.Domain) diretamente.
        var forbiddenNamespaces = new[]
        {
            "Agendio.Modules.Scheduling.Domain",
            "Agendio.Modules.Scheduling.Application",
            "Agendio.Modules.Scheduling.Infrastructure",
            "Agendio.Modules.Scheduling.Endpoints",
            "Agendio.Modules.Scheduling.DependencyInjection",
        };

        var result = Types.InAssembly(CustomersAssembly)
            .That().ResideInNamespace("Agendio.Modules.Customers")
            .ShouldNot().HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Customers so pode depender do .Contracts de Scheduling, nunca do modulo inteiro. " +
            "Tipos violando a regra: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Customers_Module_Should_Not_Depend_On_Tenancy_Internals()
    {
        // Fase 11 (fidelidade): Customers le limiar/descricao da recompensa via
        // ITenantLookupService.GetLoyaltySettingsAsync — nunca deveria enxergar
        // Tenant (Tenancy.Domain) diretamente.
        var forbiddenNamespaces = new[]
        {
            "Agendio.Modules.Tenancy.Domain",
            "Agendio.Modules.Tenancy.Application",
            "Agendio.Modules.Tenancy.Infrastructure",
            "Agendio.Modules.Tenancy.Endpoints",
            "Agendio.Modules.Tenancy.DependencyInjection",
        };

        var result = Types.InAssembly(CustomersAssembly)
            .That().ResideInNamespace("Agendio.Modules.Customers")
            .ShouldNot().HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Customers so pode depender do .Contracts de Tenancy, nunca do modulo inteiro. " +
            "Tipos violando a regra: " + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
