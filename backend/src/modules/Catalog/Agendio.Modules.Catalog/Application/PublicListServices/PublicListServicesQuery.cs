using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Catalog.Application.PublicListServices;

/// <summary>TenantId vem da rota publica — ver IHasExplicitTenant.</summary>
public sealed record PublicListServicesQuery(Guid TenantId) : IQuery<IReadOnlyList<PublicServiceSummary>>, IHasExplicitTenant;

public sealed record PublicServiceSummary(
    Guid Id, string Name, string? Description, int DurationMinutes, decimal Price, string Currency, string? Category);
