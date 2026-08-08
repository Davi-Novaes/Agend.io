using Agendio.Modules.Financeiro.Infrastructure.Persistence;
using Agendio.Modules.Resources.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Financeiro.Application.UpsertCommissionRule;

public sealed class UpsertCommissionRuleCommandHandler(
    FinanceiroDbContext dbContext, IResourceLookupService resourceLookupService, ITenantContext tenantContext)
    : ICommandHandler<UpsertCommissionRuleCommand>
{
    public async Task<Result> Handle(UpsertCommissionRuleCommand request, CancellationToken cancellationToken)
    {
        // Comissao so faz sentido para um profissional (Resource tipo Person) —
        // sala/equipamento nao "ganha" comissao. Validado aqui (Application), nao
        // no dominio: exige uma leitura sincrona cross-modulo via .Contracts.
        var resource = await resourceLookupService.FindByIdAsync(request.ResourceId, cancellationToken);
        if (resource is null || !string.Equals(resource.Type, "Person", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(Error.Validation(
                "CommissionRule.ResourceNotEligible", "So e possivel configurar comissao para um profissional (recurso do tipo Pessoa)."));
        }

        var existingRule = await dbContext.CommissionRules
            .SingleOrDefaultAsync(c => c.ResourceId == request.ResourceId, cancellationToken);

        if (existingRule is not null)
        {
            var updateResult = existingRule.UpdateRule(request.CalculationType, request.Value);
            if (updateResult.IsFailure)
            {
                return updateResult;
            }

            existingRule.Activate();
        }
        else
        {
            var createResult = Domain.CommissionRule.Create(
                tenantContext.TenantId, request.ResourceId, request.CalculationType, request.Value);

            if (createResult.IsFailure)
            {
                return Result.Failure(createResult.Error);
            }

            dbContext.CommissionRules.Add(createResult.Value);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
