using Agendio.Modules.Tenancy.Domain;

namespace Agendio.UnitTests.Tenancy;

public class TenantPaymentSettingsTests
{
    private static Tenant CreateTenant() =>
        Tenant.Create("Barbearia Modelo", "barbearia-modelo", BusinessType.Barbershop, "America/Sao_Paulo").Value;

    [Fact]
    public void New_Tenant_Should_Not_Require_Payment_By_Default()
    {
        var tenant = CreateTenant();

        tenant.PaymentRequirement.ShouldBe(PaymentRequirement.None);
        tenant.DepositPercentage.ShouldBe(30);
    }

    [Fact]
    public void UpdatePaymentSettings_Should_Succeed_With_A_Valid_Percentage()
    {
        var tenant = CreateTenant();

        var result = tenant.UpdatePaymentSettings(PaymentRequirement.Deposit, 50);

        result.IsSuccess.ShouldBeTrue();
        tenant.PaymentRequirement.ShouldBe(PaymentRequirement.Deposit);
        tenant.DepositPercentage.ShouldBe(50);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void UpdatePaymentSettings_Should_Fail_When_Requiring_Deposit_With_An_Out_Of_Range_Percentage(int percentage)
    {
        var tenant = CreateTenant();

        var result = tenant.UpdatePaymentSettings(PaymentRequirement.Deposit, percentage);

        result.IsFailure.ShouldBeTrue();
        tenant.PaymentRequirement.ShouldBe(PaymentRequirement.None);
    }

    [Fact]
    public void UpdatePaymentSettings_Should_Not_Validate_Percentage_When_Requirement_Is_None()
    {
        var tenant = CreateTenant();

        var result = tenant.UpdatePaymentSettings(PaymentRequirement.None, 0);

        result.IsSuccess.ShouldBeTrue();
        tenant.PaymentRequirement.ShouldBe(PaymentRequirement.None);
    }
}
