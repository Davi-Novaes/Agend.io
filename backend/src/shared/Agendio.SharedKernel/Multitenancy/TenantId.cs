using Agendio.SharedKernel.Primitives;

namespace Agendio.SharedKernel.Multitenancy;

/// <summary>
/// Identificador de tenant. Vive no SharedKernel (e nao em Agendio.Modules.Tenancy)
/// porque e um conceito transversal: toda entidade ITenantOwned de qualquer modulo
/// precisa dele, e nenhum modulo deveria depender de Tenancy so por causa do tipo do Id.
/// </summary>
public sealed record TenantId(Guid Value) : TypedId(Value)
{
    public static readonly TenantId Empty = new(Guid.Empty);

    public static TenantId New() => new(Guid.NewGuid());

    public static TenantId From(Guid value) => new(value);
}
