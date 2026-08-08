using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Marketing.Domain;

public sealed record CampaignId(Guid Value) : TypedId(Value)
{
    public static CampaignId New() => new(Guid.NewGuid());

    public static CampaignId From(Guid value) => new(value);
}
