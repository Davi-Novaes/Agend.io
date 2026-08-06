namespace Agendio.SharedKernel.Multitenancy;

/// <summary>
/// Marca um Command/Query cujo TenantId ja e conhecido no momento da chamada
/// (veio no corpo da requisicao), tipicamente porque a rota e publica e roda
/// antes de existir JWT (ex.: RegisterUserCommand, LoginCommand — ambos recebem
/// o TenantId do estabelecimento em que o usuario esta se cadastrando/entrando).
/// O ExplicitTenantBehavior (Agendio.Infrastructure) usa isto para ancorar o
/// tenant no ITenantContext automaticamente antes do handler tocar o banco.
/// </summary>
public interface IHasExplicitTenant
{
    Guid TenantId { get; }
}
