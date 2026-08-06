using Microsoft.AspNetCore.Routing;

namespace Agendio.Infrastructure.Endpoints;

/// <summary>
/// Cada modulo implementa isto para expor os proprios endpoints Minimal API.
/// Agendio.Api descobre todas as implementacoes via DI e mapeia no startup — o
/// host nunca precisa conhecer de antemao a lista de rotas de cada modulo.
///
/// Vive em Infrastructure (nao em Agendio.Api) porque um modulo NUNCA pode
/// referenciar o host — e o host que referencia os modulos. Infrastructure e o
/// unico lugar comum aos dois lados que ja tem FrameworkReference ao ASP.NET Core.
/// </summary>
public interface IEndpointModule
{
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
