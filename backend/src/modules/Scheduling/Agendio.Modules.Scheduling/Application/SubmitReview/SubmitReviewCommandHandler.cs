using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Application.SubmitReview;

public sealed class SubmitReviewCommandHandler(SchedulingDbContext dbContext, ICustomerLookupService customerLookup)
    : ICommandHandler<SubmitReviewCommand>
{
    private static readonly Error IdentityMismatchError = Error.Validation(
        "Review.IdentityMismatch", "Nao foi possivel confirmar sua identidade para este agendamento.");

    public async Task<Result> Handle(SubmitReviewCommand request, CancellationToken cancellationToken)
    {
        var appointment = await dbContext.Appointments
            .SingleOrDefaultAsync(a => a.Id == AppointmentId.From(request.AppointmentId), cancellationToken);

        if (appointment is null)
        {
            return Result.Failure(Error.NotFound("Appointment.NotFound", "Agendamento nao encontrado."));
        }

        if (appointment.Status != AppointmentStatus.Completed)
        {
            return Result.Failure(Error.Validation("Review.AppointmentNotCompleted", "Este agendamento ainda nao foi concluido."));
        }

        var alreadyReviewed = await dbContext.Reviews.AnyAsync(r => r.AppointmentId == appointment.Id, cancellationToken);
        if (alreadyReviewed)
        {
            return Result.Failure(Error.Validation("Review.AlreadySubmitted", "Este agendamento ja foi avaliado."));
        }

        var customer = await customerLookup.FindByIdAsync(appointment.CustomerId, cancellationToken);
        if (customer?.Email is null || !string.Equals(customer.Email.Trim(), request.CustomerEmail.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(IdentityMismatchError);
        }

        var reviewResult = Review.Create(
            appointment.TenantId, appointment.Id, appointment.CustomerId, appointment.ResourceId, appointment.ServiceName,
            request.Rating, request.Comment);

        if (reviewResult.IsFailure)
        {
            return Result.Failure(reviewResult.Error);
        }

        dbContext.Reviews.Add(reviewResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
