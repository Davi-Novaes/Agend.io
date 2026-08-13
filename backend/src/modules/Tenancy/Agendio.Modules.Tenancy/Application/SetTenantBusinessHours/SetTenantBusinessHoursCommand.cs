using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.SetTenantBusinessHours;

public sealed record SetTenantBusinessHoursCommand(IReadOnlyList<BusinessHoursEntryDto> Entries) : ICommand;
