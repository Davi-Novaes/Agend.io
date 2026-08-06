using Agendio.SharedKernel.Results;

namespace Agendio.SharedKernel.Messaging;

/// <summary>
/// Porta de entrada unica para comandos e consultas. Os endpoints da API dependem
/// so disso — nunca de um handler especifico — para nao criar acoplamento entre
/// a camada HTTP e a implementacao interna de cada modulo.
/// </summary>
public interface IDispatcher
{
    Task<Result> Send(ICommand command, CancellationToken cancellationToken = default);

    Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

    Task<Result<TResponse>> Query<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);
}
