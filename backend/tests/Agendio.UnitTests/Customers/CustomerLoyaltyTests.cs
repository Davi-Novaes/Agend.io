using Agendio.Modules.Customers.Domain;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.UnitTests.Customers;

public class CustomerLoyaltyTests
{
    private static readonly TenantId Tenant = TenantId.From(Guid.NewGuid());

    private static Customer NewCustomer() =>
        Customer.Create(Tenant, "Cliente Fidelidade", null, null, null, null).Value;

    [Fact]
    public void EarnLoyaltyPoints_Should_Increase_Balance()
    {
        var customer = NewCustomer();

        var result = customer.EarnLoyaltyPoints(1);

        result.IsSuccess.ShouldBeTrue();
        customer.LoyaltyPoints.ShouldBe(1);
    }

    [Fact]
    public void EarnLoyaltyPoints_Should_Accumulate_Across_Multiple_Calls()
    {
        var customer = NewCustomer();

        customer.EarnLoyaltyPoints(1);
        customer.EarnLoyaltyPoints(1);
        customer.EarnLoyaltyPoints(1);

        customer.LoyaltyPoints.ShouldBe(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EarnLoyaltyPoints_Should_Fail_When_Points_Is_Zero_Or_Negative(int points)
    {
        var customer = NewCustomer();

        var result = customer.EarnLoyaltyPoints(points);

        result.IsFailure.ShouldBeTrue();
        customer.LoyaltyPoints.ShouldBe(0);
    }

    [Fact]
    public void RedeemLoyaltyReward_Should_Decrease_Balance_When_Enough_Points()
    {
        var customer = NewCustomer();
        customer.EarnLoyaltyPoints(10);

        var result = customer.RedeemLoyaltyReward(10);

        result.IsSuccess.ShouldBeTrue();
        customer.LoyaltyPoints.ShouldBe(0);
    }

    [Fact]
    public void RedeemLoyaltyReward_Should_Fail_When_Not_Enough_Points()
    {
        var customer = NewCustomer();
        customer.EarnLoyaltyPoints(5);

        var result = customer.RedeemLoyaltyReward(10);

        result.IsFailure.ShouldBeTrue();
        customer.LoyaltyPoints.ShouldBe(5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RedeemLoyaltyReward_Should_Fail_When_Cost_Is_Zero_Or_Negative(int cost)
    {
        var customer = NewCustomer();
        customer.EarnLoyaltyPoints(10);

        var result = customer.RedeemLoyaltyReward(cost);

        result.IsFailure.ShouldBeTrue();
        customer.LoyaltyPoints.ShouldBe(10);
    }

    [Fact]
    public void RecordEarned_Should_Succeed_With_Valid_Data()
    {
        var occurredAtUtc = DateTimeOffset.UtcNow;
        var appointmentId = Guid.NewGuid();
        var customerId = CustomerId.New();

        var result = LoyaltyPointsLedgerEntry.RecordEarned(Tenant, customerId, 1, appointmentId, occurredAtUtc);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Kind.ShouldBe(LoyaltyPointsLedgerEntryKind.Earned);
        result.Value.Points.ShouldBe(1);
        result.Value.AppointmentId.ShouldBe(appointmentId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RecordEarned_Should_Fail_When_Points_Is_Zero_Or_Negative(int points)
    {
        var result = LoyaltyPointsLedgerEntry.RecordEarned(Tenant, CustomerId.New(), points, Guid.NewGuid(), DateTimeOffset.UtcNow);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void RecordRedeemed_Should_Succeed_With_Null_AppointmentId()
    {
        var result = LoyaltyPointsLedgerEntry.RecordRedeemed(Tenant, CustomerId.New(), 10, DateTimeOffset.UtcNow);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Kind.ShouldBe(LoyaltyPointsLedgerEntryKind.Redeemed);
        result.Value.AppointmentId.ShouldBeNull();
    }
}
