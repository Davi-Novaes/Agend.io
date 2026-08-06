namespace Agendio.SharedKernel.Results;

/// <inheritdoc cref="Result"/>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error) : base(isSuccess, error) =>
        _value = value;

    /// <summary>
    /// Acessar Value de um resultado com falha e erro de programacao (o chamador
    /// deveria ter checado IsSuccess antes) — por isso lanca, em vez de devolver
    /// default silenciosamente.
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Nao e possivel acessar o valor de um resultado com falha.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
