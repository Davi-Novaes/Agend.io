using Agendio.Modules.Financeiro.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Financeiro.Application.DeactivateCommissionRule;

public sealed class DeactivateCommissionRuleCommandHandler(FinanceiroDbContext dbContext) : ICommandHandler<DeactivateCommissionRuleCommand>
{
    public async Task<Result> Handle(DeactivateCommissionRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await dbContext.CommissionRules
            .SingleOrDefaultAsync(c => c.ResourceId == request.ResourceId, cancellationToken);

        if (rule is null)
        {
            return Result.Failure(Error.NotFound("CommissionRule.NotFound", "Regra de comissao nao encontrada para este profissional."));
        }

        rule.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
