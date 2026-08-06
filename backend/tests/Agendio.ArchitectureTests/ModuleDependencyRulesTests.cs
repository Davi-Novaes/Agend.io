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

    [Theory]
    [InlineData("Agendio.Modules.Identity.Domain")]
    [InlineData("Agendio.Modules.Tenancy.Domain")]
    public void Domain_Should_Not_Depend_On_Application_Or_Infrastructure(string domainNamespace)
    {
        var assembly = domainNamespace.Contains("Identity") ? IdentityAssembly : TenancyAssembly;
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
}
