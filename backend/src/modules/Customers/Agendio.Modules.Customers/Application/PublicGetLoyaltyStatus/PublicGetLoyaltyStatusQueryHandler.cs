using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Customers.Application.PublicGetLoyaltyStatus;

/// <summary>
/// Primeiro endpoint publico "buscar por e-mail" do projeto — anti-enumeracao
/// de proposito: e-mail invalido, programa desligado e cliente inexistente
/// retornam o MESMO erro generico, para nao revelar se um e-mail pertence a um
/// cliente deste tenant. Rate limiting global (ver Program.cs) e a defesa
/// complementar contra tentativa de forca bruta.
/// </summary>
public sealed class PublicGetLoyaltyStatusQueryHandler(CustomersDbContext dbContext, ITenantLookupService tenantLookup)
    : IQueryHandler<PublicGetLoyaltyStatusQuery, PublicLoyaltyStatus>
{
    private static readonly Error NotFoundError = Error.NotFound(
        "Customer.LoyaltyStatusNotFound", "Nao encontramos pontos de fidelidade para esse e-mail.");

    public async Task<Result<PublicLoyaltyStatus>> Handle(PublicGetLoyaltyStatusQuery request, CancellationToken cancellationToken)
    {
        var tenantId = TenantId.From(request.TenantId);

        var loyaltySettings = await tenantLookup.GetLoyaltySettingsAsync(tenantId, cancellationToken);
        if (loyaltySettings is null || !loyaltySettings.LoyaltyProgramEnabled)
        {
            return Result.Failure<PublicLoyaltyStatus>(NotFoundError);
        }

        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<PublicLoyaltyStatus>(NotFoundError);
        }

        var customer = await dbContext.Customers.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Email == emailResult.Value, cancellationToken);

        if (customer is null)
        {
            return Result.Failure<PublicLoyaltyStatus>(NotFoundError);
        }

        return Result.Success(new PublicLoyaltyStatus(
            customer.FullName, customer.LoyaltyPoints, loyaltySettings.LoyaltyVisitsForReward, loyaltySettings.LoyaltyRewardDescription));
    }
}
