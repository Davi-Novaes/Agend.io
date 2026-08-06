using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Identity.Contracts;

/// <summary>
/// Exposto em Contracts (nao em Domain) porque outros modulos vao precisar
/// referenciar "o usuario responsavel por X" no futuro (ex.: Resources.Staff
/// apontando para a conta que faz login) sem enxergar o agregado User inteiro.
/// </summary>
public sealed record UserId(Guid Value) : TypedId(Value)
{
    public static UserId New() => new(Guid.NewGuid());

    public static UserId From(Guid value) => new(value);
}
