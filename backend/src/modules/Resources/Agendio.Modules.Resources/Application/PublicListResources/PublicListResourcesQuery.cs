using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Resources.Application.PublicListResources;

/// <summary>TenantId vem da rota publica — ver IHasExplicitTenant.</summary>
public sealed record PublicListResourcesQuery(Guid TenantId) : IQuery<IReadOnlyList<PublicResourceSummary>>, IHasExplicitTenant;

public sealed record PublicResourceSummary(Guid Id, string Name, string Type, string? Description);
