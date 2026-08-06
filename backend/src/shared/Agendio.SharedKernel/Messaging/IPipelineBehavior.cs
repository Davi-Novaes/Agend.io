namespace Agendio.SharedKernel.Messaging;

/// <summary>
/// Comportamento transversal que envolve a execucao de um handler (validacao,
/// log, transacao...). Implementacoes concretas (ex.: ValidationBehavior com
/// FluentValidation) vivem em Agendio.Infrastructure, ja que dependem de
/// bibliotecas externas — esta interface fica pura no SharedKernel.
/// </summary>
public interface IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, Func<Task<TResponse>> next, CancellationToken cancellationToken);
}
