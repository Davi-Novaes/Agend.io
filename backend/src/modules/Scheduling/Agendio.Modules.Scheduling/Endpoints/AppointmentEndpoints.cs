using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Scheduling.Application.CancelAppointment;
using Agendio.Modules.Scheduling.Application.CompleteAppointment;
using Agendio.Modules.Scheduling.Application.ConfirmAppointment;
using Agendio.Modules.Scheduling.Application.GetAppointmentById;
using Agendio.Modules.Scheduling.Application.GetAppointmentStats;
using Agendio.Modules.Scheduling.Application.ListAppointments;
using Agendio.Modules.Scheduling.Application.ListNotificationLog;
using Agendio.Modules.Scheduling.Application.MarkAppointmentNoShow;
using Agendio.Modules.Scheduling.Application.RescheduleAppointment;
using Agendio.Modules.Scheduling.Application.ScheduleAppointment;
using Agendio.Modules.Scheduling.Application.StartAppointment;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Agendio.Modules.Scheduling.Endpoints;

public sealed class AppointmentEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/appointments").WithTags("Scheduling").RequireAuthorization();

        group.MapGet("/", async (DateTimeOffset from, DateTimeOffset to, Guid? resourceId, Guid? unitId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new ListAppointmentsQuery(from, to, resourceId, unitId), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("ListAppointments")
        .WithSummary("Lista agendamentos que se sobrepoem a janela [from, to), opcionalmente filtrando por recurso e/ou unidade.");

        group.MapGet("/stats", async (DateOnly from, DateOnly to, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetAppointmentStatsQuery(from, to), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("GetAppointmentStats")
        .WithSummary("Estatisticas de agendamentos no periodo: conclusao, no-show, cancelamento e faturamento por servico/profissional.");

        group.MapGet("/notifications", async (int page, int pageSize, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new ListNotificationLogQuery(page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("ListNotificationLog")
        .WithSummary("Historico de mensagens (e-mail/WhatsApp) enviadas aos clientes, paginado, mais recentes primeiro (Fase 7).");

        group.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetAppointmentByIdQuery(id), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("GetAppointmentById")
        .WithSummary("Busca um agendamento pelo Id.");

        group.MapPost("/", async (ScheduleAppointmentRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new ScheduleAppointmentCommand(request.CustomerId, request.ResourceId, request.ServiceId, request.StartAtUtc, request.Notes);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/appointments/{result.Value}", new { id = result.Value })
                : result.Error.ToProblemResult();
        })
        .WithName("ScheduleAppointment")
        .WithSummary("Cria um novo agendamento. Rejeita com 409 se o horario acabou de ser reservado por outra requisicao.");

        group.MapPost("/{id:guid}/confirm", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new ConfirmAppointmentCommand(id), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("ConfirmAppointment")
        .WithSummary("Confirma um agendamento Agendado.");

        group.MapPost("/{id:guid}/start", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new StartAppointmentCommand(id), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("StartAppointment")
        .WithSummary("Marca o inicio do atendimento (Em andamento).");

        group.MapPost("/{id:guid}/complete", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new CompleteAppointmentCommand(id), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("CompleteAppointment")
        .WithSummary("Marca o agendamento como concluido.");

        group.MapPost("/{id:guid}/no-show", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new MarkAppointmentNoShowCommand(id), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("MarkAppointmentNoShow")
        .WithSummary("Marca que o cliente nao compareceu.");

        group.MapPost("/{id:guid}/cancel", async (Guid id, CancelAppointmentRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new CancelAppointmentCommand(id, request.ByStaff), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("CancelAppointment")
        .WithSummary("Cancela um agendamento, pelo cliente ou pela equipe.");

        group.MapPut("/{id:guid}/reschedule", async (Guid id, RescheduleAppointmentRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new RescheduleAppointmentCommand(id, request.NewStartAtUtc), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("RescheduleAppointment")
        .WithSummary("Remarca um agendamento, preservando a duracao original. Rejeita com 409 se o novo horario ja estiver ocupado.");
    }

    private sealed record ScheduleAppointmentRequest(Guid CustomerId, Guid ResourceId, Guid ServiceId, DateTimeOffset StartAtUtc, string? Notes);

    private sealed record CancelAppointmentRequest(bool ByStaff);

    private sealed record RescheduleAppointmentRequest(DateTimeOffset NewStartAtUtc);
}
