using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantSchedulingSettings;

public sealed record ClosedDateDto(DateOnly Date, string? Reason);

// Sem TenantId: vem de ITenantContext (claim do JWT), como em UpdateTenantProfileCommand.
public sealed record UpdateTenantSchedulingSettingsCommand(
    IReadOnlyList<ClosedDateDto> ClosedDates, int AppointmentBufferMinutes) : ICommand;
