using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Scheduling.Application.PublicScheduleAppointment;

/// <summary>TenantId vem da rota publica — ver IHasExplicitTenant.</summary>
public sealed record PublicScheduleAppointmentCommand(
    Guid TenantId,
    Guid ResourceId,
    Guid ServiceId,
    DateTimeOffset StartAtUtc,
    string CustomerFullName,
    string CustomerEmail,
    string? CustomerPhone,
    string? Notes,
    // So obrigatorio quando o tenant exige sinal (Fase 16) — validado no handler,
    // nao no validator, porque depende de config do tenant carregada em runtime.
    string? CustomerCpf = null) : ICommand<PublicScheduleAppointmentResult>, IHasExplicitTenant;

/// <summary>PaymentUrl vem preenchido so quando o tenant exige sinal E a cobranca foi gerada com sucesso.</summary>
public sealed record PublicScheduleAppointmentResult(Guid AppointmentId, string? PaymentUrl);
