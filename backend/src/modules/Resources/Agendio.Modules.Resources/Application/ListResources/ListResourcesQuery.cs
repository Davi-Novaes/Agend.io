using Agendio.Modules.Resources.Domain;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Resources.Application.ListResources;

public sealed record ListResourcesQuery(string? Search, int Page = 1, int PageSize = 20) : IQuery<ListResourcesResult>;

public sealed record ResourceSummary(Guid Id, string Name, ResourceType Type, int Capacity, string? Description, bool IsActive);

public sealed record ListResourcesResult(IReadOnlyList<ResourceSummary> Items, int TotalCount, int Page, int PageSize);
