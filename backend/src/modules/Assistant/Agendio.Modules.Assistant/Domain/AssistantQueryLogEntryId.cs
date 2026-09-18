using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Assistant.Domain;

public sealed record AssistantQueryLogEntryId(Guid Value) : TypedId(Value)
{
    public static AssistantQueryLogEntryId New() => new(Guid.NewGuid());

    public static AssistantQueryLogEntryId From(Guid value) => new(value);
}
