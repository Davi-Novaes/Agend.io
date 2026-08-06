using Agendio.SharedKernel.Results;
using Microsoft.AspNetCore.Http;

namespace Agendio.Infrastructure.Endpoints;

/// <summary>
/// Traduz Error -> status HTTP num unico lugar. O handler de dominio nunca
/// decide status HTTP (ver ErrorType) — so classifica o erro por categoria.
/// </summary>
public static class ErrorHttpExtensions
{
    public static IResult ToProblemResult(this Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(title: error.Code, detail: error.Message, statusCode: statusCode);
    }
}
