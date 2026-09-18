using Agendio.Modules.Tenancy.Domain;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.UnitTests.Tenancy;

public class UnitTests
{
    private static readonly TenantId Tenant = TenantId.From(Guid.NewGuid());

    [Fact]
    public void Create_Should_Fail_When_Name_Is_Empty()
    {
        var result = Unit.Create(Tenant, "   ", null, null, null, null);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Succeed_With_Valid_Data()
    {
        var result = Unit.Create(Tenant, "Unidade Centro", "Rua Principal, 123", "Sao Paulo", "SP", "Brasil");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Unidade Centro");
        result.Value.Address.ShouldBe("Rua Principal, 123");
        result.Value.City.ShouldBe("Sao Paulo");
        result.Value.State.ShouldBe("SP");
        result.Value.Country.ShouldBe("Brasil");
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Trim_Name_And_Address()
    {
        var result = Unit.Create(Tenant, "  Unidade Centro  ", "  Rua Principal, 123  ", "  Sao Paulo  ", "  SP  ", "  Brasil  ");

        result.Value.Name.ShouldBe("Unidade Centro");
        result.Value.Address.ShouldBe("Rua Principal, 123");
        result.Value.City.ShouldBe("Sao Paulo");
        result.Value.State.ShouldBe("SP");
        result.Value.Country.ShouldBe("Brasil");
    }

    [Fact]
    public void Update_Should_Fail_When_Name_Is_Empty()
    {
        var unit = Unit.Create(Tenant, "Unidade Centro", null, null, null, null).Value;

        var result = unit.Update("  ", null, null, null, null);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Update_Should_Change_Name_Address_And_Location()
    {
        var unit = Unit.Create(Tenant, "Unidade Centro", null, null, null, null).Value;

        var result = unit.Update("Unidade Centro Renovada", "Nova Rua, 456", "Rio de Janeiro", "RJ", "Brasil");

        result.IsSuccess.ShouldBeTrue();
        unit.Name.ShouldBe("Unidade Centro Renovada");
        unit.Address.ShouldBe("Nova Rua, 456");
        unit.City.ShouldBe("Rio de Janeiro");
        unit.State.ShouldBe("RJ");
        unit.Country.ShouldBe("Brasil");
    }

    [Fact]
    public void Activate_Deactivate_Should_Toggle_IsActive()
    {
        var unit = Unit.Create(Tenant, "Unidade Centro", null, null, null, null).Value;

        unit.Deactivate();
        unit.IsActive.ShouldBeFalse();

        unit.Activate();
        unit.IsActive.ShouldBeTrue();
    }
}
