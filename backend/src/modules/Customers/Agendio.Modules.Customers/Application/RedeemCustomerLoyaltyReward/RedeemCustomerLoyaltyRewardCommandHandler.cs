using Agendio.Modules.Customers.Domain;
using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Customers.Application.RedeemCustomerLoyaltyReward;

public sealed class RedeemCustomerLoyaltyRewardCommandHandler(
    CustomersDbContext dbContext, ITenantContext tenantContext, ITenantLookupService tenantLookup, IClock clock)
    : ICommandHandler<RedeemCustomerLoyaltyRewardCommand>
{
    public async Task<Result> Handle(RedeemCustomerLoyaltyRewardCommand request, CancellationToken cancellationToken)
    {
        var loyaltySettings = await tenantLookup.GetLoyaltySettingsAsync(tenantContext.TenantId, cancellationToken);
        if (loyaltySettings is null || !loyaltySettings.LoyaltyProgramEnabled)
        {
            return Result.Failure(Error.Validation("Customer.LoyaltyProgramDisabled", "O programa de fidelidade nao esta ativo para este estabelecimento."));
        }

        var customer = await dbContext.Customers
            .SingleOrDefaultAsync(c => c.Id == CustomerId.From(request.CustomerId), cancellationToken);

        if (customer is null)
        {
            return Result.Failure(Error.NotFound("Customer.NotFound", "Cliente nao encontrado."));
        }

        var redeemResult = customer.RedeemLoyaltyReward(loyaltySettings.LoyaltyVisitsForReward);
        if (redeemResult.IsFailure)
        {
            return redeemResult;
        }

        var ledgerEntryResult = LoyaltyPointsLedgerEntry.RecordRedeemed(
            tenantContext.TenantId, customer.Id, loyaltySettings.LoyaltyVisitsForReward, clock.UtcNow);
        if (ledgerEntryResult.IsFailure)
        {
            return Result.Failure(ledgerEntryResult.Error);
        }

        dbContext.LoyaltyPointsLedgerEntries.Add(ledgerEntryResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
