using Agendio.Modules.Resources.Domain;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.UnitTests.Resources;

public class TimeOffTests
{
    private static readonly TenantId Tenant = TenantId.From(Guid.NewGuid());
    private static readonly ResourceId Resource = ResourceId.New();

    [Fact]
    public void Create_Should_Succeed_With_Valid_Range()
    {
        var start = new DateOnly(2026, 8, 20);
        var end = new DateOnly(2026, 8, 22);

        var result = TimeOff.Create(Tenant, Resource, start, end, "Ferias");

        result.IsSuccess.ShouldBeTrue();
        result.Value.StartDate.ShouldBe(start);
        result.Value.EndDate.ShouldBe(end);
        result.Value.Reason.ShouldBe("Ferias");
    }

    [Fact]
    public void Create_Should_Accept_A_Single_Day_Range()
    {
        var day = new DateOnly(2026, 8, 20);

        var result = TimeOff.Create(Tenant, Resource, day, day, null);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Fail_When_EndDate_Is_Before_StartDate()
    {
        var result = TimeOff.Create(Tenant, Resource, new DateOnly(2026, 8, 22), new DateOnly(2026, 8, 20), null);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Store_Null_Reason_When_Blank()
    {
        var day = new DateOnly(2026, 8, 20);

        var result = TimeOff.Create(Tenant, Resource, day, day, "   ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Reason.ShouldBeNull();
    }
}
