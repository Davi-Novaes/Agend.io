using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Scheduling.Application.GetAvailableSlots;
using Agendio.Modules.Scheduling.Application.PublicScheduleAppointment;
using Agendio.Modules.Scheduling.Application.SubmitReview;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Agendio.Modules.Scheduling.Endpoints;

/// <summary>Superficie anonima consumida pelo portal publico do cliente (Sprint 4) — ver PublicCatalogEndpoints.</summary>
public sealed class PublicAppointmentEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/public/tenants/{tenantId:guid}").WithTags("Public Scheduling");

        group.MapGet("/availability", async (Guid tenantId, Guid resourceId, Guid serviceId, DateOnly date, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetAvailableSlotsQuery(tenantId, resourceId, serviceId, date), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("GetAvailableSlots")
        .WithSummary("Calcula os horarios livres de um recurso para um servico em uma data.");

        group.MapPost("/appointments", async (Guid tenantId, PublicScheduleAppointmentRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new PublicScheduleAppointmentCommand(
                tenantId, request.ResourceId, request.ServiceId, request.StartAtUtc,
                request.CustomerFullName, request.CustomerEmail, request.CustomerPhone, request.Notes);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/public/tenants/{tenantId}/appointments/{result.Value}", new { id = result.Value })
                : result.Error.ToProblemResult();
        })
        .WithName("PublicScheduleAppointment")
        .WithSummary("Cria um agendamento pelo portal publico, sem exigir login previo do cliente.");

        group.MapPost("/appointments/{appointmentId:guid}/review", async (
            Guid tenantId, Guid appointmentId, SubmitReviewRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new SubmitReviewCommand(tenantId, appointmentId, request.CustomerEmail, request.Rating, request.Comment);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("SubmitReview")
        .WithSummary("Cliente avalia um atendimento concluido, confirmando a identidade pelo e-mail cadastrado (Fase 12).");
    }

    private sealed record PublicScheduleAppointmentRequest(
        Guid ResourceId, Guid ServiceId, DateTimeOffset StartAtUtc, string CustomerFullName, string CustomerEmail, string? CustomerPhone, string? Notes);

    private sealed record SubmitReviewRequest(string CustomerEmail, int Rating, string? Comment);
}
