namespace Agendio.SharedKernel.Messaging;

/// <summary>
/// Interface tecnica que o Dispatcher usa para resolver o handler certo via DI.
/// Handlers de aplicacao implementam ICommandHandler/IQueryHandler, nao esta
/// diretamente — mas como ambas herdam desta, o Dispatcher trata as duas igual.
/// </summary>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
