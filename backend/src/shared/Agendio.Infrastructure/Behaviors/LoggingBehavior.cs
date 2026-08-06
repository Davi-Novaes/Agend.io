using System.Diagnostics;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Agendio.Infrastructure.Behaviors;

/// <summary>Log estruturado de todo comando/consulta: nome, duracao e resultado (sucesso/erro de negocio/excecao).</summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, Func<Task<TResponse>> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();

            if (response is Result { IsFailure: true } failure)
            {
                logger.LogWarning(
                    "{RequestName} falhou em {ElapsedMs}ms: [{ErrorCode}] {ErrorMessage}",
                    requestName, stopwatch.ElapsedMilliseconds, failure.Error.Code, failure.Error.Message);
            }
            else
            {
                logger.LogInformation("{RequestName} concluido em {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{RequestName} lancou excecao apos {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
