using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.Modules.Scheduling.Contracts;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Customers.Application.CustomerPortal;

public sealed class GetCustomerPortalQueryHandler(
    ICustomerPortalAccessStore accessStore,
    CustomersDbContext dbContext,
    ICustomerPortalAppointmentsLookupService appointmentsLookup,
    ITenantLookupService tenantLookup) : IQueryHandler<GetCustomerPortalQuery, CustomerPortalProfile>
{
    public async Task<Result<CustomerPortalProfile>> Handle(
        GetCustomerPortalQuery request, CancellationToken cancellationToken)
    {
        var customerId = await accessStore.GetCustomerIdAsync(request.TenantId, request.SessionToken);
        if (customerId is null)
        {
            return Unauthorized();
        }

        var customer = await dbContext.Customers.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == Domain.CustomerId.From(customerId.Value), cancellationToken);
        if (customer is null || !customer.IsActive || customer.Email is null)
        {
            return Unauthorized();
        }

        var appointments = await appointmentsLookup.ListForCustomerAsync(customerId.Value, cancellationToken);
        var loyalty = await tenantLookup.GetLoyaltySettingsAsync(customer.TenantId, cancellationToken);

        return Result.Success(new CustomerPortalProfile(
            customer.FullName,
            customer.Email.Value,
            customer.Phone?.Value,
            customer.LoyaltyPoints,
            loyalty?.LoyaltyProgramEnabled == true ? loyalty.LoyaltyVisitsForReward : null,
            loyalty?.LoyaltyProgramEnabled == true ? loyalty.LoyaltyRewardDescription : null,
            appointments.Select(appointment => new CustomerPortalAppointment(
                appointment.Id,
                appointment.ServiceId,
                appointment.ServiceName,
                appointment.ResourceName,
                appointment.StartAtUtc,
                appointment.EndAtUtc,
                appointment.Price,
                appointment.Currency,
                appointment.Status)).ToList()));
    }

    private static Result<CustomerPortalProfile> Unauthorized() => Result.Failure<CustomerPortalProfile>(
        Error.Unauthorized("CustomerPortal.Unauthorized", "Sua sessão expirou. Entre novamente para continuar."));
}
