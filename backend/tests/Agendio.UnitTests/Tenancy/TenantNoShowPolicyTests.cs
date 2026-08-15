using Agendio.Modules.Tenancy.Domain;

namespace Agendio.UnitTests.Tenancy;

public class TenantNoShowPolicyTests
{
    private static Tenant CreateTenant() =>
        Tenant.Create("Barbearia Modelo", "barbearia-modelo", BusinessType.Barbershop, "America/Sao_Paulo").Value;

    [Fact]
    public void New_Tenant_Should_Have_The_Policy_Disabled_By_Default()
    {
        var tenant = CreateTenant();

        tenant.RequireDepositAfterNoShows.ShouldBeFalse();
        tenant.NoShowThresholdForDeposit.ShouldBe(2);
    }

    [Fact]
    public void UpdateNoShowPolicy_Should_Succeed_With_A_Valid_Threshold()
    {
        var tenant = CreateTenant();

        var result = tenant.UpdateNoShowPolicy(true, 3);

        result.IsSuccess.ShouldBeTrue();
        tenant.RequireDepositAfterNoShows.ShouldBeTrue();
        tenant.NoShowThresholdForDeposit.ShouldBe(3);
    }

    [Fact]
    public void UpdateNoShowPolicy_Should_Fail_When_Threshold_Is_Zero_Or_Negative()
    {
        var tenant = CreateTenant();

        var result = tenant.UpdateNoShowPolicy(true, 0);

        result.IsFailure.ShouldBeTrue();
        tenant.NoShowThresholdForDeposit.ShouldBe(2);
    }
}
