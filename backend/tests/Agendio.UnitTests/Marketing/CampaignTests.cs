using Agendio.Modules.Marketing.Domain;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.UnitTests.Marketing;

public class CampaignTests
{
    private static readonly TenantId Tenant = TenantId.From(Guid.NewGuid());

    [Fact]
    public void Create_Should_Fail_When_Subject_Is_Empty()
    {
        var result = Campaign.Create(Tenant, "   ", "Corpo da campanha", CampaignChannel.Email, null, 3, DateTimeOffset.UtcNow);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Fail_When_Body_Is_Empty()
    {
        var result = Campaign.Create(Tenant, "Assunto", "   ", CampaignChannel.Email, null, 3, DateTimeOffset.UtcNow);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Fail_When_RecipientCount_Is_Negative()
    {
        var result = Campaign.Create(Tenant, "Assunto", "Corpo", CampaignChannel.Email, null, -1, DateTimeOffset.UtcNow);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Succeed_With_Valid_Data()
    {
        var sentAtUtc = DateTimeOffset.UtcNow;

        var result = Campaign.Create(
            Tenant, "  Promocao de agosto  ", "  Aproveite  ", CampaignChannel.WhatsApp, "Vip", 5, sentAtUtc);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Subject.ShouldBe("Promocao de agosto");
        result.Value.Body.ShouldBe("Aproveite");
        result.Value.Channel.ShouldBe(CampaignChannel.WhatsApp);
        result.Value.TargetSegment.ShouldBe("Vip");
        result.Value.RecipientCount.ShouldBe(5);
        result.Value.SentAtUtc.ShouldBe(sentAtUtc);
    }

    [Fact]
    public void Create_Should_Allow_Zero_Recipients()
    {
        var result = Campaign.Create(Tenant, "Assunto", "Corpo", CampaignChannel.Email, null, 0, DateTimeOffset.UtcNow);

        result.IsSuccess.ShouldBeTrue();
        result.Value.RecipientCount.ShouldBe(0);
    }
}
