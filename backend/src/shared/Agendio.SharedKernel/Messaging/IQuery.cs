using Agendio.SharedKernel.Results;

namespace Agendio.SharedKernel.Messaging;

/// <summary>Consulta: nunca altera estado, sempre devolve um dado.</summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
