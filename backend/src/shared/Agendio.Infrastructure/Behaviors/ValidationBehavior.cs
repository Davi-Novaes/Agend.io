using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using FluentValidation;

namespace Agendio.Infrastructure.Behaviors;

/// <summary>
/// Roda todo IValidator&lt;TRequest&gt; registrado antes do handler. Falha de
/// validacao nunca chega ao handler — e traduzida direto em Result de falha,
/// entao o handler so ve entrada ja validada.
///
/// TResponse e sempre Result ou Result&lt;T&gt; (garantido pelos construtores de
/// ICommand/ICommand&lt;T&gt;/IQuery&lt;T&gt;), entao existe sempre um jeito de construir
/// uma resposta de falha do tipo certo — ver FailureFactory.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly Lazy<Func<Error, TResponse>> FailureFactory = new(BuildFailureFactory);

    public async Task<TResponse> Handle(TRequest request, Func<Task<TResponse>> next, CancellationToken cancellationToken)
    {
        var validatorList = validators.ToList();

        if (validatorList.Count == 0)
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validatorList.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var failures = validationResults.SelectMany(r => r.Errors).ToList();

        if (failures.Count == 0)
        {
            return await next();
        }

        var message = string.Join(" | ", failures.Select(f => f.ErrorMessage));
        var error = Error.Validation("Validation.Failed", message);

        return FailureFactory.Value(error);
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
            $"ValidationBehavior nao sabe construir uma resposta de falha para o tipo {responseType.Name}. " +
            "TResponse deveria ser sempre Result ou Result<T>.");
    }
}
