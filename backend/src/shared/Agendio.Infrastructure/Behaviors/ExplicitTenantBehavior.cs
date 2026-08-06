using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Infrastructure.Behaviors;

/// <summary>
/// Ancora o ITenantContext ANTES do handler tocar o banco, para todo comando que
/// implementa IHasExplicitTenant (registro de usuario, login). Sem isto, essas
/// operacoes falhariam: o Global Query Filter e a Row Level Security bloqueariam
/// a propria leitura/escrita necessaria para autenticar, ja que nao ha JWT ainda.
/// Roda antes do ValidationBehavior seria ideal, mas a ordem entre eles nao
/// importa aqui — nenhum validator depende do tenant estar ancorado.
/// </summary>
public sealed class ExplicitTenantBehavior<TRequest, TResponse>(ITenantContext tenantContext) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest request, Func<Task<TResponse>> next, CancellationToken cancellationToken)
    {
        if (request is IHasExplicitTenant explicitTenant)
        {
            tenantContext.SetTenant(TenantId.From(explicitTenant.TenantId));
        }

        return next();
    }
}
