using Agendio.Modules.Financeiro.Infrastructure.Persistence;
using Agendio.Modules.Resources.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Financeiro.Application.ListCommissionRules;

public sealed class ListCommissionRulesQueryHandler(FinanceiroDbContext dbContext, IResourceLookupService resourceLookupService)
    : IQueryHandler<ListCommissionRulesQuery, IReadOnlyList<CommissionRuleSummary>>
{
    public async Task<Result<IReadOnlyList<CommissionRuleSummary>>> Handle(ListCommissionRulesQuery request, CancellationToken cancellationToken)
    {
        // Todo profissional (recurso tipo Person) aparece na lista, com ou sem
        // regra configurada — assim o dono ve quem ainda nao tem comissao definida.
        var professionals = await resourceLookupService.ListActiveByTypeAsync("Person", cancellationToken);

        var rules = await dbContext.CommissionRules.AsNoTracking().ToDictionaryAsync(c => c.ResourceId, cancellationToken);

        var summaries = professionals
            .Select(professional => rules.TryGetValue(professional.ResourceId, out var rule)
                ? new CommissionRuleSummary(professional.ResourceId, professional.Name, rule.CalculationType, rule.Value, rule.IsActive)
                : new CommissionRuleSummary(professional.ResourceId, professional.Name, null, null, false))
            .ToList();

        return Result.Success<IReadOnlyList<CommissionRuleSummary>>(summaries);
    }
}
