using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Agendio.Api;

/// <summary>
/// Unico ponto que decide o que uma excecao NAO TRATADA vira na resposta HTTP.
/// Antes disto nao existia — em Development, a Developer Exception Page do
/// ASP.NET Core devolvia stack trace completo, caminho de arquivo do servidor
/// e ATE OS HEADERS DA PROPRIA REQUISICAO (incluindo o Bearer token do
/// chamador) no corpo da resposta; sem "app.UseExceptionHandler()" explicito
/// (ver Program.cs), uma unica config errada de ASPNETCORE_ENVIRONMENT em
/// producao reabriria o mesmo vazamento pra qualquer excecao. Ver BL-05, docs/BACKLOG.md
/// (achados P1-1 do Backend audit e o trio C5-bad/F3/F4 do QA audit — mesma
/// causa raiz: nenhum handler global).
///
/// Excecao de negocio (regra violada) nunca passa por aqui — isso e Result/
/// Error.ToProblemResult() no handler, por design do projeto (CLAUDE.md:
/// "excecao e para o excepcional, nao para regra de negocio violada"). Este
/// handler so existe pra excecao GENUINAMENTE inesperada (bug, infra fora do
/// ar, corpo JSON malformado antes do model binding terminar) nunca vazar
/// detalhe interno — sempre o mesmo formato ProblemDetails do resto da API.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // BadHttpRequestException e o que o Minimal API lanca quando o corpo
        // JSON e invalido (UTF-8 quebrado, enum fora do dominio, DateOnly em
        // formato errado) ANTES do handler/FluentValidation rodarem — o unico
        // caso em que uma excecao nao tratada corresponde a erro do CLIENTE
        // (400), nao um bug nosso (500).
        var isClientError = exception is BadHttpRequestException;
        var statusCode = isClientError ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;

        logger.LogError(
            exception,
            "Excecao nao tratada em {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Title = isClientError ? "Requisicao invalida." : "Erro interno.",
                Detail = isClientError
                    ? "O corpo da requisicao esta malformado ou tem um valor invalido."
                    : "Ocorreu um erro inesperado. Tente novamente em instantes.",
                Status = statusCode,
            },
            cancellationToken);

        return true;
    }
}
