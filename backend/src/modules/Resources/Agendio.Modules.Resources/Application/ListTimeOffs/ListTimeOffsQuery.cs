using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Resources.Application.ListTimeOffs;

public sealed record ListTimeOffsQuery(Guid ResourceId) : IQuery<IReadOnlyList<TimeOffSummary>>;

public sealed record TimeOffSummary(Guid Id, DateOnly StartDate, DateOnly EndDate, string? Reason);
