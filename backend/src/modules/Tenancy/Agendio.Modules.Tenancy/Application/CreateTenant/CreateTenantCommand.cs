using Agendio.Modules.Tenancy.Domain;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.CreateTenant;

public sealed record CreateTenantCommand(string Name, string Slug, BusinessType BusinessType, string TimeZoneId) : ICommand<Guid>;
