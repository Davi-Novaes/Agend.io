using Agendio.Modules.Financeiro.Domain;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Financeiro.Application.CreateAccountPayable;

// Sem TenantId: vem de ITenantContext (claim do JWT). Sempre um lancamento
// MANUAL — ResourceId/SourceAppointmentId ficam null (a origem automatica, via
// comissao, so acontece dentro do FinancialIntegrationEventConsumer).
public sealed record CreateAccountPayableCommand(
    string Description, decimal Amount, DateOnly DueDate, ExpenseCategory Category) : ICommand<Guid>;
