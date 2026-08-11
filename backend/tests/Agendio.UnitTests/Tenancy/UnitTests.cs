using Agendio.Modules.Tenancy.Domain;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.UnitTests.Tenancy;

public class UnitTests
{
    private static readonly TenantId Tenant = TenantId.From(Guid.NewGuid());

    [Fact]
    public void Create_Should_Fail_When_Name_Is_Empty()
    {
        var result = Unit.Create(Tenant, "   ", null);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Succeed_With_Valid_Data()
    {
        var result = Unit.Create(Tenant, "Unidade Centro", "Rua Principal, 123");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Unidade Centro");
        result.Value.Address.ShouldBe("Rua Principal, 123");
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Trim_Name_And_Address()
    {
        var result = Unit.Create(Tenant, "  Unidade Centro  ", "  Rua Principal, 123  ");

        result.Value.Name.ShouldBe("Unidade Centro");
        result.Value.Address.ShouldBe("Rua Principal, 123");
    }

    [Fact]
    public void Update_Should_Fail_When_Name_Is_Empty()
    {
        var unit = Unit.Create(Tenant, "Unidade Centro", null).Value;

        var result = unit.Update("  ", null);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Update_Should_Change_Name_And_Address()
    {
        var unit = Unit.Create(Tenant, "Unidade Centro", null).Value;

        var result = unit.Update("Unidade Centro Renovada", "Nova Rua, 456");

        result.IsSuccess.ShouldBeTrue();
        unit.Name.ShouldBe("Unidade Centro Renovada");
        unit.Address.ShouldBe("Nova Rua, 456");
    }

    [Fact]
    public void Activate_Deactivate_Should_Toggle_IsActive()
    {
        var unit = Unit.Create(Tenant, "Unidade Centro", null).Value;

        unit.Deactivate();
        unit.IsActive.ShouldBeFalse();

        unit.Activate();
        unit.IsActive.ShouldBeTrue();
    }
}
