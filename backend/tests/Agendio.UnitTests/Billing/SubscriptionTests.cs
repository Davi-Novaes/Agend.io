using Agendio.Modules.Billing.Domain;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.UnitTests.Billing;

public class SubscriptionTests
{
    private static readonly TenantId Tenant = TenantId.From(Guid.NewGuid());
    private static readonly PlanId Plan = PlanId.New();

    [Fact]
    public void Cancel_Should_Deactivate_Immediately_When_Trialing()
    {
        var now = DateTimeOffset.UtcNow;
        var subscription = Subscription.StartTrial(Tenant, Plan, now);

        var result = subscription.Cancel(now);

        result.IsSuccess.ShouldBeTrue();
        subscription.Status.ShouldBe(SubscriptionStatus.Canceled);
        subscription.CanceledAtUtc.ShouldBe(now);
    }

    [Fact]
    public void Cancel_Should_Keep_Access_Until_Paid_Period_Ends_When_Active()
    {
        var now = DateTimeOffset.UtcNow;
        var subscription = Subscription.StartTrial(Tenant, Plan, now);
        subscription.MarkActive(now.AddDays(30));

        var result = subscription.Cancel(now);

        result.IsSuccess.ShouldBeTrue();
        subscription.Status.ShouldBe(SubscriptionStatus.Active);
        subscription.CanceledAtUtc.ShouldBe(now);
        subscription.CurrentPeriodEndsAtUtc.ShouldBe(now.AddDays(30));
    }

    [Fact]
    public void Cancel_Should_Deactivate_Immediately_When_Active_Without_A_Paid_Period_In_Progress()
    {
        var now = DateTimeOffset.UtcNow;
        var subscription = Subscription.StartTrial(Tenant, Plan, now);
        subscription.ActivateAsFree(Plan);

        var result = subscription.Cancel(now);

        result.IsSuccess.ShouldBeTrue();
        subscription.Status.ShouldBe(SubscriptionStatus.Canceled);
    }

    [Fact]
    public void Cancel_Should_Fail_When_Already_Canceled()
    {
        var now = DateTimeOffset.UtcNow;
        var subscription = Subscription.StartTrial(Tenant, Plan, now);
        subscription.Cancel(now);

        var result = subscription.Cancel(now);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Cancel_Should_Fail_When_Already_Pending_Cancellation()
    {
        var now = DateTimeOffset.UtcNow;
        var subscription = Subscription.StartTrial(Tenant, Plan, now);
        subscription.MarkActive(now.AddDays(30));
        subscription.Cancel(now);

        var result = subscription.Cancel(now);

        result.IsFailure.ShouldBeTrue();
        subscription.Status.ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public void FinalizeCancellation_Should_Close_A_Pending_Cancellation()
    {
        var now = DateTimeOffset.UtcNow;
        var subscription = Subscription.StartTrial(Tenant, Plan, now);
        subscription.MarkActive(now.AddDays(30));
        subscription.Cancel(now);

        subscription.FinalizeCancellation();

        subscription.Status.ShouldBe(SubscriptionStatus.Canceled);
    }

    [Fact]
    public void Reactivate_Should_Clear_A_Pending_Cancellation()
    {
        var now = DateTimeOffset.UtcNow;
        var subscription = Subscription.StartTrial(Tenant, Plan, now);
        subscription.MarkActive(now.AddDays(30));
        subscription.Cancel(now);

        subscription.Reactivate(now.AddDays(60));

        subscription.Status.ShouldBe(SubscriptionStatus.Active);
        subscription.CanceledAtUtc.ShouldBeNull();
    }
}
