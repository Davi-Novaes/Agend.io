namespace Agendio.SharedKernel.Primitives;

/// <summary>
/// Base para identificadores fortemente tipados. Sem isso, todo Id vira um Guid
/// solto e nada impede passar um TenantId onde se espera um UserId — o bug so
/// aparece em runtime, e as vezes so em producao, cruzando dados entre tenants.
/// Com TypedId, e o compilador que barra, nao um teste.
///
/// Cada modulo declara seus proprios ids como record selado herdando desta base:
/// <c>public sealed record UserId(Guid Value) : TypedId(Value);</c>
/// </summary>
public abstract record TypedId(Guid Value)
{
    public sealed override string ToString() => Value.ToString();
}
