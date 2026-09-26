using Agendio.Infrastructure.Security;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Security;

namespace Agendio.Infrastructure.Behaviors;

/// <summary>
/// Roda ANTES do handler para todo Command que implementa
/// IRequiresTurnstileVerification (login/cadastro publicos) — falha rapido
/// contra bot/credential-stuffing sem gastar o custo do Argon2id nem tocar
/// o banco. TResponse e sempre Result ou Result&lt;T&gt;, mesma garantia
/// documentada em ValidationBehavior.
/// </summary>
public sealed class TurnstileVerificationBehavior<TRequest, TResponse>(ITurnstileVerifier verifier)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly Lazy<Func<Error, TResponse>> FailureFactory = new(BuildFailureFactory);
    private static readonly Error VerificationFailedError = Error.Validation(
        "Turnstile.Failed", "Nao foi possivel confirmar que voce e humano. Recarregue a pagina e tente de novo.");

    public async Task<TResponse> Handle(TRequest request, Func<Task<TResponse>> next, CancellationToken cancellationToken)
    {
        if (request is IRequiresTurnstileVerification turnstileRequest)
        {
            var isHuman = await verifier.VerifyAsync(turnstileRequest.TurnstileToken, cancellationToken);
            if (!isHuman)
            {
                return FailureFactory.Value(VerificationFailedError);
            }
        }

        return await next();
    }

    private static Func<Error, TResponse> BuildFailureFactory()
    {
        var responseType = typeof(TResponse);

        if (responseType == typeof(Result))
        {
            return error => (TResponse)(object)Result.Failure(error);
        }

        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = responseType.GetGenericArguments()[0];
            var genericFailureMethod = typeof(Result)
                .GetMethods()
                .Single(m => m is { Name: nameof(Result.Failure), IsGenericMethodDefinition: true })
                .MakeGenericMethod(valueType);

            return error => (TResponse)genericFailureMethod.Invoke(null, [error])!;
        }

        throw new InvalidOperationException(
            $"TurnstileVerificationBehavior nao sabe construir uma resposta de falha para o tipo {responseType.Name}. " +
            "TResponse deveria ser sempre Result ou Result<T>.");
    }
}
