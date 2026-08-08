using Agendio.Modules.Financeiro.Domain;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Financeiro.Application.ListCommissionRules;

public sealed record ListCommissionRulesQuery : IQuery<IReadOnlyList<CommissionRuleSummary>>;

// CalculationType/Value/IsActive nulos = profissional sem regra configurada
// ainda (comportamento invisivel ate o dono decidir configurar).
public sealed record CommissionRuleSummary(
    Guid ResourceId, string ResourceName, CommissionCalculationType? CalculationType, decimal? Value, bool IsActive);
