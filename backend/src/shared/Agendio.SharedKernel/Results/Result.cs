namespace Agendio.SharedKernel.Results;

/// <summary>
/// Resultado de uma operacao que pode falhar por motivo de negocio. Handlers de
/// comando/consulta retornam Result em vez de lancar excecao — o chamador e
/// obrigado a lidar com a falha, o compilador nao deixa esquecer um try/catch.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    protected internal Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("Um resultado de sucesso nao pode carregar um erro.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("Um resultado de falha precisa de um erro.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}
