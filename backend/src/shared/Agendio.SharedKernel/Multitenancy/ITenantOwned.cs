namespace Agendio.SharedKernel.Multitenancy;

/// <summary>
/// Marca uma entidade como pertencente a um tenant especifico. Toda entidade
/// ITenantOwned DEVE ter um EF Core global query filter por TenantId e Row Level
/// Security habilitada na tabela — isso e verificado automaticamente em
/// Agendio.ArchitectureTests; esquecer quebra o build, nao vira bug em producao.
/// </summary>
public interface ITenantOwned
{
    TenantId TenantId { get; }
}
