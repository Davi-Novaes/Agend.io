using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Scheduling.Application.ProcessAppointmentDepositWebhook;

/// <summary>
/// TenantId e DepositId vem do externalReference que NOS embutimos ao criar a
/// cobranca ({tenantId}:{depositId}) — a Asaas nao sabe o que e um "tenant",
/// so ecoa o valor de volta no webhook. IHasExplicitTenant ancora o
/// ITenantContext antes do handler tocar o banco (o webhook nao tem JWT).
/// </summary>
public sealed record ProcessAppointmentDepositWebhookCommand(Guid TenantId, Guid DepositId, string EventType) : ICommand, IHasExplicitTenant;
