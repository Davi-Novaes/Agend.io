using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Tenancy.Application.CreateTenant;
using Agendio.Modules.Tenancy.Application.CreateUnit;
using Agendio.Modules.Tenancy.Application.GetPublicTenantProfile;
using Agendio.Modules.Tenancy.Application.GetTenantProfile;
using Agendio.Modules.Tenancy.Application.GetUnitById;
using Agendio.Modules.Tenancy.Application.ListUnits;
using Agendio.Modules.Tenancy.Application.SetTenantBusinessHours;
using Agendio.Modules.Tenancy.Application.SetUnitActiveStatus;
using Agendio.Modules.Tenancy.Application.UpdateTenantBanner;
using Agendio.Modules.Tenancy.Application.UpdateTenantBranding;
using Agendio.Modules.Tenancy.Application.UpdateTenantLogo;
using Agendio.Modules.Tenancy.Application.UpdateTenantProfile;
using Agendio.Modules.Tenancy.Application.UpdateUnit;
using Agendio.Modules.Tenancy.Domain;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Agendio.Modules.Tenancy.Endpoints;

public sealed class TenancyEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tenants").WithTags("Tenancy");

        group.MapGet("/business-types", () => Results.Ok(BusinessTypeCatalog.All.Select(ToResponse)))
        // Publica: o onboarding precisa listar os segmentos ANTES do cadastro existir.
        .AllowAnonymous()
        .WithName("ListBusinessTypes")
        .WithSummary("Lista os segmentos disponiveis, cada um com seu vocabulario (Business Type Templates).");

        group.MapPost("/", async (CreateTenantRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new CreateTenantCommand(request.Name, request.Slug, request.BusinessType, request.TimeZoneId);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/tenants/{result.Value}", new { id = result.Value })
                : result.Error.ToProblemResult();
        })
        // Criacao de tenant e publica no Sprint 0 para provar o fluxo ponta a
        // ponta. O onboarding self-service com verificacao de e-mail/documento
        // entra no Sprint 1 — ainda sera uma rota publica, mas com mais controles.
        .AllowAnonymous()
        .WithName("CreateTenant")
        .WithSummary("Cria um novo estabelecimento (tenant).");

        group.MapGet("/by-slug/{slug}", async (string slug, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetPublicTenantProfileQuery(slug), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        // Publica de proposito: a pagina publica (Fase 2) e o portal do cliente
        // precisam resolver o tenant pelo slug ANTES de qualquer autenticacao.
        .AllowAnonymous()
        .WithName("GetTenantBySlug")
        .WithSummary("Resolve o perfil publico de um estabelecimento pelo identificador (slug): dados de vitrine, contato e horario de funcionamento.");

        group.MapPut("/branding", async (UpdateBrandingRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new UpdateTenantBrandingCommand(request.PrimaryColorHex);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        // "Owner" e um literal (nao Identity.Domain.UserRole) de proposito: um
        // modulo nunca referencia outro, so .Contracts — e nao vale a pena
        // criar um contrato so por causa de uma string de role.
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("UpdateTenantBranding")
        .WithSummary("Atualiza a cor de marca do estabelecimento (rejeitada se o contraste AA nao for atingido).");

        group.MapPost("/logo", async (IFormFile file, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await using var contentStream = new MemoryStream();
            await file.CopyToAsync(contentStream, cancellationToken);

            var command = new UpdateTenantLogoCommand(contentStream.ToArray(), file.ContentType);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.Ok(new { logoUrl = result.Value }) : result.Error.ToProblemResult();
        })
        .DisableAntiforgery()
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("UpdateTenantLogo")
        .WithSummary("Faz upload do logo do estabelecimento (PNG/JPEG/WEBP, ate 2MB).");

        group.MapPost("/banner", async (IFormFile file, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await using var contentStream = new MemoryStream();
            await file.CopyToAsync(contentStream, cancellationToken);

            var command = new UpdateTenantBannerCommand(contentStream.ToArray(), file.ContentType);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.Ok(new { bannerUrl = result.Value }) : result.Error.ToProblemResult();
        })
        .DisableAntiforgery()
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("UpdateTenantBanner")
        .WithSummary("Faz upload do banner (capa) da pagina publica do estabelecimento (PNG/JPEG/WEBP, ate 4MB).");

        group.MapGet("/profile", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetTenantProfileQuery(), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("GetTenantProfile")
        .WithSummary("Retorna os dados completos de perfil do estabelecimento (para preencher o formulario de edicao).");

        group.MapPut("/profile", async (UpdateProfileRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new UpdateTenantProfileCommand(
                request.Description, request.Phone, request.WhatsApp, request.Email, request.Address, request.InstagramUrl,
                request.FacebookUrl);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("UpdateTenantProfile")
        .WithSummary("Atualiza descricao, contato, endereco e redes sociais do estabelecimento.");

        group.MapPut("/business-hours", async (SetBusinessHoursRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var entries = request.Entries.Select(e => new BusinessHoursEntryDto(e.DayOfWeek, e.StartTime, e.EndTime)).ToList();
            var result = await dispatcher.Send(new SetTenantBusinessHoursCommand(entries), cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("SetTenantBusinessHours")
        .WithSummary("Substitui o horario de funcionamento do estabelecimento (semana inteira de uma vez).");

        var units = endpoints.MapGroup("/api/units").WithTags("Units").RequireAuthorization(policy => policy.RequireRole("Owner"));

        units.MapGet("/", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new ListUnitsQuery(), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("ListUnits")
        .WithSummary("Lista as unidades do estabelecimento.");

        units.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetUnitByIdQuery(id), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("GetUnitById")
        .WithSummary("Busca uma unidade pelo Id.");

        units.MapPost("/", async (CreateUnitRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new CreateUnitCommand(request.Name, request.Address);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/units/{result.Value}", new { id = result.Value })
                : result.Error.ToProblemResult();
        })
        .WithName("CreateUnit")
        .WithSummary("Cadastra uma nova unidade do estabelecimento.");

        units.MapPut("/{id:guid}", async (Guid id, UpdateUnitRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new UpdateUnitCommand(id, request.Name, request.Address);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("UpdateUnit")
        .WithSummary("Atualiza os dados de uma unidade.");

        units.MapPatch("/{id:guid}/status", async (Guid id, SetUnitStatusRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new SetUnitActiveStatusCommand(id, request.IsActive), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("SetUnitActiveStatus")
        .WithSummary("Ativa ou desativa uma unidade.");
    }

    private sealed record CreateUnitRequest(string Name, string? Address);

    private sealed record UpdateUnitRequest(string Name, string? Address);

    private sealed record SetUnitStatusRequest(bool IsActive);

    private static object ToResponse(BusinessTypeDefinition definition) => new
    {
        value = definition.BusinessType.ToString(),
        definition.DisplayName,
        terminology = definition.Terminology,
    };

    private sealed record CreateTenantRequest(string Name, string Slug, BusinessType BusinessType, string TimeZoneId);

    private sealed record UpdateBrandingRequest(string PrimaryColorHex);

    private sealed record UpdateProfileRequest(
        string? Description, string? Phone, string? WhatsApp, string? Email, string? Address, string? InstagramUrl, string? FacebookUrl);

    private sealed record BusinessHoursEntryRequest(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

    private sealed record SetBusinessHoursRequest(IReadOnlyList<BusinessHoursEntryRequest> Entries);
}
