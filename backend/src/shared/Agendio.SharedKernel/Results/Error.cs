namespace Agendio.SharedKernel.Results;

/// <summary>
/// Erro de negocio, nao excecao. Regra violada nao e "excepcional" — e o
/// comportamento esperado de um handler quando a entrada ou o estado nao permitem
/// a operacao. Excecao fica reservada para o que e de fato excepcional (falha de
/// infraestrutura, bug).
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
}
