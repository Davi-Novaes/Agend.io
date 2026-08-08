using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Financeiro.Application.DeactivateCommissionRule;

public sealed record DeactivateCommissionRuleCommand(Guid ResourceId) : ICommand;
