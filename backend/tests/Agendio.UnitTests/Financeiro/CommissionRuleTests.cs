using Agendio.Modules.Financeiro.Domain;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.UnitTests.Financeiro;

public class CommissionRuleTests
{
    private static readonly TenantId Tenant = TenantId.From(Guid.NewGuid());
    private static readonly Guid ResourceId = Guid.NewGuid();

    [Fact]
    public void Create_Should_Fail_When_Value_Is_Negative()
    {
        var result = CommissionRule.Create(Tenant, ResourceId, CommissionCalculationType.Percentage, -1m);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Fail_When_Percentage_Above_100()
    {
        var result = CommissionRule.Create(Tenant, ResourceId, CommissionCalculationType.Percentage, 101m);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Allow_FixedAmount_Above_100()
    {
        var result = CommissionRule.Create(Tenant, ResourceId, CommissionCalculationType.FixedAmount, 500m);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Start_Active()
    {
        var result = CommissionRule.Create(Tenant, ResourceId, CommissionCalculationType.Percentage, 10m);

        result.IsSuccess.ShouldBeTrue();
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void CalculateCommission_Should_Compute_Percentage_Of_Service_Price()
    {
        var rule = CommissionRule.Create(Tenant, ResourceId, CommissionCalculationType.Percentage, 20m).Value;
        var servicePrice = Money.Create(100m).Value;

        var commission = rule.CalculateCommission(servicePrice);

        commission.Amount.ShouldBe(20m);
    }

    [Fact]
    public void CalculateCommission_Should_Return_Fixed_Value_Regardless_Of_Service_Price()
    {
        var rule = CommissionRule.Create(Tenant, ResourceId, CommissionCalculationType.FixedAmount, 15m).Value;
        var servicePrice = Money.Create(200m).Value;

        var commission = rule.CalculateCommission(servicePrice);

        commission.Amount.ShouldBe(15m);
    }

    [Fact]
    public void Deactivate_Should_Turn_IsActive_Off()
    {
        var rule = CommissionRule.Create(Tenant, ResourceId, CommissionCalculationType.Percentage, 10m).Value;

        rule.Deactivate();

        rule.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void UpdateRule_Should_Reject_Invalid_Value_Without_Mutating_Existing_State()
    {
        var rule = CommissionRule.Create(Tenant, ResourceId, CommissionCalculationType.Percentage, 10m).Value;

        var result = rule.UpdateRule(CommissionCalculationType.Percentage, 150m);

        result.IsFailure.ShouldBeTrue();
        rule.Value.ShouldBe(10m);
    }
}
