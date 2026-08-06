namespace Agendio.SharedKernel.Primitives;

/// <summary>
/// Base para toda entidade do dominio. Identidade (Id) define igualdade, nao os
/// valores dos demais campos — duas entidades com o mesmo Id sao a mesma entidade
/// mesmo que um campo tenha mudado.
/// </summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    public TId Id { get; protected init; } = default!;

    protected Entity(TId id) => Id = id;

    // Construtor sem argumentos exigido pelo EF Core para materializar entidades
    // vindas do banco sem passar pelos construtores de negocio.
    protected Entity()
    {
    }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);
}
