using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Feedback.Domain;

public sealed record FeedbackEntryId(Guid Value) : TypedId(Value)
{
    public static FeedbackEntryId New() => new(Guid.NewGuid());

    public static FeedbackEntryId From(Guid value) => new(value);
}
