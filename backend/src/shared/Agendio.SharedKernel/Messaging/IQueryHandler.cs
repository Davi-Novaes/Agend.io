using Agendio.SharedKernel.Results;

namespace Agendio.SharedKernel.Messaging;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
