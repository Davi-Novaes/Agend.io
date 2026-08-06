using System.Collections.Concurrent;
using Agendio.SharedKernel.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.SharedKernel.Messaging;

/// <summary>
/// Dispatcher de CQRS proprio — substitui o MediatR, que passou a exigir licenca
/// comercial (ver docs/adr). Resolve o handler via DI, monta a cadeia de
/// IPipelineBehavior em volta dele e invoca. ~100 linhas, sem reflection pesada
/// (o wrapper generico e cacheado por tipo de request).
/// </summary>
public sealed class Dispatcher(IServiceProvider serviceProvider) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, Type> WrapperTypeCache = new();

    public Task<Result> Send(ICommand command, CancellationToken cancellationToken = default) =>
        SendInternal<Result>(command, cancellationToken);

    public Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default) =>
        SendInternal<Result<TResponse>>(command, cancellationToken);

    public Task<Result<TResponse>> Query<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) =>
        SendInternal<Result<TResponse>>(query, cancellationToken);

    private Task<TResponse> SendInternal<TResponse>(object request, CancellationToken cancellationToken)
    {
        var requestType = request.GetType();
        var wrapperType = WrapperTypeCache.GetOrAdd(
            requestType,
            static requestType => typeof(HandlerWrapper<,>).MakeGenericType(requestType, typeof(TResponse)));

        var wrapper = (HandlerWrapperBase<TResponse>)Activator.CreateInstance(wrapperType)!;
        return wrapper.Handle(request, serviceProvider, cancellationToken);
    }

    private abstract class HandlerWrapperBase<TResponse>
    {
        public abstract Task<TResponse> Handle(object request, IServiceProvider provider, CancellationToken cancellationToken);
    }

    private sealed class HandlerWrapper<TRequest, TResponse> : HandlerWrapperBase<TResponse>
        where TRequest : IRequest<TResponse>
    {
        public override async Task<TResponse> Handle(object request, IServiceProvider provider, CancellationToken cancellationToken)
        {
            var typedRequest = (TRequest)request;
            var handler = provider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
            var behaviors = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse().ToArray();

            var pipeline = () => handler.Handle(typedRequest, cancellationToken);

            foreach (var behavior in behaviors)
            {
                var next = pipeline;
                pipeline = () => behavior.Handle(typedRequest, next, cancellationToken);
            }

            return await pipeline().ConfigureAwait(false);
        }
    }
}
