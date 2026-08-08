using Agendio.Modules.Financeiro.Domain;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Financeiro.Application.UpsertCommissionRule;

public sealed record UpsertCommissionRuleCommand(Guid ResourceId, CommissionCalculationType CalculationType, decimal Value) : ICommand;
