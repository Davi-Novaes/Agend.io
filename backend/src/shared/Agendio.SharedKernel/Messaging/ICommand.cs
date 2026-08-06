using Agendio.SharedKernel.Results;

namespace Agendio.SharedKernel.Messaging;

/// <summary>Comando que altera estado e nao devolve dado alem de sucesso/falha.</summary>
public interface ICommand : IRequest<Result>;

/// <summary>Comando que altera estado e devolve um dado (ex.: o Id criado).</summary>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>;
