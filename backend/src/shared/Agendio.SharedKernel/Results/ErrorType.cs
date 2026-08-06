namespace Agendio.SharedKernel.Results;

/// <summary>
/// Categoria do erro. O host mapeia cada categoria para um status HTTP fixo
/// (ver ProblemDetails no Agendio.Api) — o handler de dominio nunca decide status HTTP.
/// </summary>
public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
}
