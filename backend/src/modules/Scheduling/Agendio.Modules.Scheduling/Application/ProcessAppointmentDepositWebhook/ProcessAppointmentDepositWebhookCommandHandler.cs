using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Application.ProcessAppointmentDepositWebhook;

public sealed class ProcessAppointmentDepositWebhookCommandHandler(SchedulingDbContext dbContext, IClock clock)
    : ICommandHandler<ProcessAppointmentDepositWebhookCommand>
{
    private static readonly string[] PaidEventTypes = ["PAYMENT_CONFIRMED", "PAYMENT_RECEIVED"];

    public async Task<Result> Handle(ProcessAppointmentDepositWebhookCommand request, CancellationToken cancellationToken)
    {
        // Evento que nao muda o status de pagamento (ex.: PAYMENT_UPDATED,
        // PAYMENT_OVERDUE) — sucesso sem acao, Asaas nao precisa reenviar.
        if (!PaidEventTypes.Contains(request.EventType))
        {
            return Result.Success();
        }

        var deposit = await dbContext.AppointmentDeposits
            .SingleOrDefaultAsync(d => d.Id == AppointmentDepositId.From(request.DepositId), cancellationToken);
        if (deposit is null)
        {
            // externalReference nao bate com nenhum deposito deste tenant —
            // nada a fazer (evita retentativa eterna da Asaas por um id invalido).
            return Result.Success();
        }

        var markResult = deposit.MarkPaid(clock.UtcNow);
        if (markResult.IsFailure)
        {
            return markResult;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
