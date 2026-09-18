using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Tenancy.Application.CreateTenant;
using Agendio.Modules.Tenancy.Application.CreateUnit;
using Agendio.Modules.Tenancy.Application.GetPublicTenantProfile;
using Agendio.Modules.Tenancy.Application.GetTenantCompanyInfo;
using Agendio.Modules.Tenancy.Application.GetTenantProfile;
using Agendio.Modules.Tenancy.Application.GetUnitById;
using Agendio.Modules.Tenancy.Application.ListUnits;
using Agendio.Modules.Tenancy.Application.SetTenantBusinessHours;
using Agendio.Modules.Tenancy.Application.SetUnitActiveStatus;
using Agendio.Modules.Tenancy.Application.UpdateTenantBanner;
using Agendio.Modules.Tenancy.Application.UpdateTenantBranding;
using Agendio.Modules.Tenancy.Application.UpdateTenantCompanyInfo;
using Agendio.Modules.Tenancy.Application.UpdateTenantLogo;
using Agendio.Modules.Tenancy.Application.UpdateTenantLoyaltySettings;
using Agendio.Modules.Tenancy.Application.UpdateTenantNoShowPolicy;
using Agendio.Modules.Tenancy.Application.UpdateTenantPageCustomization;
using Agendio.Modules.Tenancy.Application.UpdateTenantPaymentSettings;
using Agendio.Modules.Tenancy.Application.UpdateTenantProfile;
using Agendio.Modules.Tenancy.Application.UpdateTenantPublicPageStatus;
using Agendio.Modules.Tenancy.Application.UpdateTenantReminderSettings;
using Agendio.Modules.Tenancy.Application.UpdateTenantSchedulingSettings;
using Agendio.Modules.Tenancy.Application.UpdateTenantWhatsAppSettings;
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
        // Sem RequireRole("Owner") DE PROPOSITO (achado numa auditoria pos-fato,
        // nao um relaxamento casual): nenhum campo aqui e sigiloso (o token do
        // WhatsApp so aparece como booleano "configurado", nunca o valor), e
        // este MESMO endpoint e a fonte de dado de Sidebar/Header/
        // TenantThemeProvider/todas as abas do hub de Configuracoes — restringir
        // a leitura a Owner (pensado so pro formulario de edicao, ver PUT
        // abaixo) deixava Staff sem cor de marca, terminologia customizada ou
        // qualquer aba de Configuracoes, sempre caindo em 403 silencioso.
        // Escrita (PUT/POST abaixo) continua Owner-only.
        .RequireAuthorization()
        .WithName("GetTenantProfile")
        .WithSummary("Retorna os dados completos de perfil do estabelecimento (leitura liberada a qualquer papel; edicao continua so-Owner).");

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

        group.MapPut("/page-customization", async (UpdatePageCustomizationRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new UpdateTenantPageCustomizationCommand(
                request.SecondaryColorHex, request.Font, request.ButtonStyle, request.ShowAboutSection, request.ShowServicesSection,
                request.ShowTeamSection, request.ShowHoursSection, request.ShowContactSection, request.HomeHeroTitle,
                request.HomeHeroDescription, request.HomeCtaText, request.BookingInstructionsText);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("UpdateTenantPageCustomization")
        .WithSummary("Atualiza a personalizacao da pagina publica: cor secundaria, fonte, estilo de botao, textos de conteudo e visibilidade de secoes.");

        group.MapGet("/company-info", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetTenantCompanyInfoQuery(), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        // Owner-only tambem na LEITURA (nao so na escrita, ver PUT abaixo) —
        // pedido explicito do usuario (2026-09-05): CNPJ/CPF/razao social/
        // localizacao cadastral do estabelecimento nao sao "so mais um campo"
        // do perfil geral (GetTenantProfile, liberado a qualquer papel) — por
        // isso viraram um endpoint proprio em vez de campos condicionais no
        // DTO grande.
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("GetTenantCompanyInfo")
        .WithSummary("Retorna os dados cadastrais do estabelecimento (nome, razao social, CNPJ/CPF, cidade, estado, CEP) — Owner-only.");

        group.MapPut("/company-info", async (UpdateCompanyInfoRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new UpdateTenantCompanyInfoCommand(request.Name, request.LegalName, request.Document, request.City, request.State, request.ZipCode);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("UpdateTenantCompanyInfo")
        .WithSummary("Atualiza os dados cadastrais do estabelecimento (nome, razao social, CNPJ/CPF, cidade, estado, CEP) — nunca exposto no perfil publico.");

        group.MapPut("/publish-status", async (UpdatePublishStatusRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new UpdateTenantPublicPageStatusCommand(request.Enabled), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("UpdateTenantPublicPageStatus")
        .WithSummary("Publica ou despublica a pagina publica do estabelecimento — independente de IsActive (exclusivo do Super Admin).");

        group.MapPut("/scheduling-settings", async (UpdateSchedulingSettingsRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var closedDates = request.ClosedDates.Select(d => new ClosedDateDto(d.Date, d.Reason)).ToList();
            var command = new UpdateTenantSchedulingSettingsCommand(closedDates, request.AppointmentBufferMinutes);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("UpdateTenantSchedulingSettings")
        .WithSummary("Atualiza datas fechadas (feriados/eventos) e o intervalo minimo entre agendamentos do estabelecimento.");

        group.MapPut("/whatsapp-settings", async (UpdateWhatsAppSettingsRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new UpdateTenantWhatsAppSettingsCommand(
                request.Enabled,
                request.PhoneNumberId,
                request.AccessToken,
                request.ScheduledTemplate,
                request.ReminderTemplate,
                request.CancelledTemplate,
                request.RescheduledTemplate,
                request.ConfirmedTemplate,
                request.CompletedTemplate);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("UpdateTenantWhatsAppSettings")
        .WithSummary("Conecta/desconecta a integracao com WhatsApp e configura os templates de mensagem (Fase 6).");

        group.MapPut("/reminder-settings", async (UpdateReminderSettingsRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new UpdateTenantReminderSettingsCommand(
                request.Reminder24hEnabled, request.Reminder2hEnabled, request.PostServiceThankYouEnabled);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("UpdateTenantReminderSettings")
        .WithSummary("Liga/desliga os lembretes automaticos (24h antes, 2h antes, pos-atendimento) do estabelecimento (Fase 7).");

        group.MapPut("/loyalty-settings", async (UpdateLoyaltySettingsRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new UpdateTenantLoyaltySettingsCommand(
                request.LoyaltyProgramEnabled, request.LoyaltyVisitsForReward, request.LoyaltyRewardDescription);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("UpdateTenantLoyaltySettings")
        .WithSummary("Configura o programa de fidelidade (ligado/desligado, visitas para a recompensa, descricao da recompensa) (Fase 11).");

        group.MapPut("/no-show-policy", async (UpdateNoShowPolicyRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new UpdateTenantNoShowPolicyCommand(request.RequireDepositAfterNoShows, request.NoShowThresholdForDeposit);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("UpdateTenantNoShowPolicy")
        .WithSummary("Configura o aviso de exigir sinal apos N faltas do cliente (Fase 15) — so sinaliza, nunca bloqueia agendamento sozinho.");

        group.MapPut("/payment-settings", async (UpdatePaymentSettingsRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new UpdateTenantPaymentSettingsCommand(request.PaymentRequired, request.DepositPercentage);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("UpdateTenantPaymentSettings")
        .WithSummary("Liga/desliga a exigencia de sinal no agendamento publico e o percentual cobrado (Fase 16).");

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
            var command = new CreateUnitCommand(request.Name, request.Address, request.City, request.State, request.Country);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/units/{result.Value}", new { id = result.Value })
                : result.Error.ToProblemResult();
        })
        .WithName("CreateUnit")
        .WithSummary("Cadastra uma nova unidade do estabelecimento.");

        units.MapPut("/{id:guid}", async (Guid id, UpdateUnitRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new UpdateUnitCommand(id, request.Name, request.Address, request.City, request.State, request.Country);
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

    private sealed record CreateUnitRequest(string Name, string? Address, string? City, string? State, string? Country);

    private sealed record UpdateUnitRequest(string Name, string? Address, string? City, string? State, string? Country);

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

    private sealed record UpdatePageCustomizationRequest(
        string? SecondaryColorHex,
        PublicPageFont Font,
        PublicPageButtonStyle ButtonStyle,
        bool ShowAboutSection,
        bool ShowServicesSection,
        bool ShowTeamSection,
        bool ShowHoursSection,
        bool ShowContactSection,
        string? HomeHeroTitle,
        string? HomeHeroDescription,
        string? HomeCtaText,
        string? BookingInstructionsText);

    private sealed record UpdateCompanyInfoRequest(string Name, string? LegalName, string? Document, string? City, string? State, string? ZipCode);

    private sealed record UpdatePublishStatusRequest(bool Enabled);

    private sealed record BusinessHoursEntryRequest(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

    private sealed record SetBusinessHoursRequest(IReadOnlyList<BusinessHoursEntryRequest> Entries);

    private sealed record ClosedDateRequest(DateOnly Date, string? Reason);

    private sealed record UpdateSchedulingSettingsRequest(IReadOnlyList<ClosedDateRequest> ClosedDates, int AppointmentBufferMinutes);

    private sealed record UpdateWhatsAppSettingsRequest(
        bool Enabled,
        string? PhoneNumberId,
        string? AccessToken,
        string? ScheduledTemplate,
        string? ReminderTemplate,
        string? CancelledTemplate,
        string? RescheduledTemplate,
        string? ConfirmedTemplate,
        string? CompletedTemplate);

    private sealed record UpdateReminderSettingsRequest(bool Reminder24hEnabled, bool Reminder2hEnabled, bool PostServiceThankYouEnabled);

    private sealed record UpdateLoyaltySettingsRequest(bool LoyaltyProgramEnabled, int LoyaltyVisitsForReward, string LoyaltyRewardDescription);

    private sealed record UpdateNoShowPolicyRequest(bool RequireDepositAfterNoShows, int NoShowThresholdForDeposit);

    private sealed record UpdatePaymentSettingsRequest(bool PaymentRequired, int DepositPercentage);
}
