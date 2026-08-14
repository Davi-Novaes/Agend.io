using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Application.ListNotificationLog;

public sealed class ListNotificationLogQueryHandler(SchedulingDbContext dbContext, ICustomerLookupService customerLookup)
    : IQueryHandler<ListNotificationLogQuery, ListNotificationLogResult>
{
    public async Task<Result<ListNotificationLogResult>> Handle(ListNotificationLogQuery request, CancellationToken cancellationToken)
    {
        // Left join com Appointments (mesmo modulo, sem violar regra de
        // dependencia) so pra mostrar o nome do servico no historico — o
        // agendamento em si nunca e apagado (cancelar so muda o Status), entao
        // na pratica o lado direito nunca fica vazio, mas o join fica seguro
        // mesmo assim.
        var query =
            from log in dbContext.NotificationLogEntries.AsNoTracking()
            join appointment in dbContext.Appointments.AsNoTracking() on log.AppointmentId equals appointment.Id into appointmentGroup
            from appointment in appointmentGroup.DefaultIfEmpty()
            where request.CustomerId == null || log.CustomerId == request.CustomerId
            orderby log.SentAtUtc descending
            select new
            {
                log.Id,
                log.AppointmentId,
                log.CustomerId,
                log.Channel,
                log.Trigger,
                log.Status,
                log.SentAtUtc,
                log.ErrorMessage,
                ServiceName = appointment != null ? appointment.ServiceName : null,
            };

        var paged = await query.ToPagedItemsAsync(request.Page, request.PageSize, cancellationToken);

        var customerNames = new Dictionary<Guid, string>();
        foreach (var customerId in paged.Items.Select(i => i.CustomerId).Distinct())
        {
            var customer = await customerLookup.FindByIdAsync(customerId, cancellationToken);
            customerNames[customerId] = customer?.FullName ?? "Cliente removido";
        }

        var items = paged.Items
            .Select(i => new NotificationLogItem(
                i.Id.Value,
                i.AppointmentId.Value,
                i.ServiceName ?? "Servico removido",
                i.CustomerId,
                customerNames[i.CustomerId],
                i.Channel.ToString(),
                i.Trigger.ToString(),
                i.Status.ToString(),
                i.SentAtUtc,
                i.ErrorMessage))
            .ToList();

        return Result.Success(new ListNotificationLogResult(items, paged.TotalCount, paged.Page, paged.PageSize));
    }
}
