using Agendio.Modules.Resources.Domain;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.UnitTests.Resources;

public class ResourceTests
{
    private static readonly TenantId Tenant = TenantId.From(Guid.NewGuid());

    private static Resource CreateResource() => Resource.Create(Tenant, "Dra. Ana", ResourceType.Person, 1, null).Value;

    [Fact]
    public void SetPhoto_Should_Set_The_Photo_Url()
    {
        var resource = CreateResource();

        var result = resource.SetPhoto("/uploads/resource-photos/abc.png");

        result.IsSuccess.ShouldBeTrue();
        resource.PhotoUrl.ShouldBe("/uploads/resource-photos/abc.png");
    }

    [Fact]
    public void SetPhoto_Should_Fail_When_Url_Is_Empty()
    {
        var resource = CreateResource();

        var result = resource.SetPhoto(" ");

        result.IsFailure.ShouldBeTrue();
        resource.PhotoUrl.ShouldBeNull();
    }

    [Fact]
    public void SetSpecialties_Should_Trim_Deduplicate_And_Drop_Blank_Entries()
    {
        var resource = CreateResource();

        resource.SetSpecialties(["  Ortodontia  ", "Clareamento", "ortodontia", "   "]);

        resource.Specialties.ShouldBe(["Ortodontia", "Clareamento"]);
    }

    [Fact]
    public void SetSpecialties_Should_Replace_The_Whole_List()
    {
        var resource = CreateResource();
        resource.SetSpecialties(["Ortodontia", "Clareamento"]);

        resource.SetSpecialties(["Implantes"]);

        resource.Specialties.ShouldBe(["Implantes"]);
    }

    [Fact]
    public void SetServiceIds_Should_Deduplicate()
    {
        var resource = CreateResource();
        var serviceId = Guid.NewGuid();

        resource.SetServiceIds([serviceId, serviceId]);

        resource.ServiceIds.ShouldBe([serviceId]);
    }

    [Fact]
    public void SetServiceIds_Should_Replace_The_Whole_List()
    {
        var resource = CreateResource();
        resource.SetServiceIds([Guid.NewGuid(), Guid.NewGuid()]);

        var newServiceId = Guid.NewGuid();
        resource.SetServiceIds([newServiceId]);

        resource.ServiceIds.ShouldBe([newServiceId]);
    }

    [Fact]
    public void SetServiceIds_With_Empty_List_Should_Mean_No_Restriction()
    {
        var resource = CreateResource();
        resource.SetServiceIds([Guid.NewGuid()]);

        resource.SetServiceIds([]);

        resource.ServiceIds.ShouldBeEmpty();
    }

    [Fact]
    public void New_Resource_Should_Inherit_Business_Hours()
    {
        var resource = CreateResource();

        resource.InheritsBusinessHours.ShouldBeTrue();
        resource.WorkingHours.ShouldBeEmpty();
    }

    [Fact]
    public void SetWorkingHours_Should_Create_A_Custom_Schedule()
    {
        var resource = CreateResource();

        var result = resource.SetWorkingHours([(DayOfWeek.Monday, new TimeOnly(7, 0), new TimeOnly(16, 0))]);

        result.IsSuccess.ShouldBeTrue();
        resource.InheritsBusinessHours.ShouldBeFalse();
    }

    [Fact]
    public void SetWorkingHours_With_Empty_List_Should_Restore_Business_Hours_Inheritance()
    {
        var resource = CreateResource();
        resource.SetWorkingHours([(DayOfWeek.Monday, new TimeOnly(7, 0), new TimeOnly(16, 0))]);

        var result = resource.SetWorkingHours([]);

        result.IsSuccess.ShouldBeTrue();
        resource.InheritsBusinessHours.ShouldBeTrue();
        resource.WorkingHours.ShouldBeEmpty();
    }
}
