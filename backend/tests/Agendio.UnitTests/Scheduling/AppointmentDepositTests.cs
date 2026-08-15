using Agendio.Modules.Scheduling.Domain;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.UnitTests.Scheduling;

public class AppointmentDepositTests
{
    private static readonly TenantId Tenant = TenantId.From(Guid.NewGuid());
    private static readonly AppointmentId Appointment = AppointmentId.New();
    private static readonly Money Amount = Money.Create(50m, "BRL").Value;

    [Fact]
    public void Create_Should_Succeed_And_Start_Pending()
    {
        var result = AppointmentDeposit.Create(Tenant, Appointment, Amount);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(AppointmentDepositStatus.Pending);
        result.Value.GatewayChargeId.ShouldBeNull();
        result.Value.InvoiceUrl.ShouldBeNull();
    }

    [Fact]
    public void Create_Should_Fail_When_Amount_Is_Zero()
    {
        var result = AppointmentDeposit.Create(Tenant, Appointment, Money.Zero());

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void AttachGatewayCharge_Should_Succeed_When_Pending()
    {
        var deposit = AppointmentDeposit.Create(Tenant, Appointment, Amount).Value;

        var result = deposit.AttachGatewayCharge("charge-1", "https://pay.example.com/1");

        result.IsSuccess.ShouldBeTrue();
        deposit.GatewayChargeId.ShouldBe("charge-1");
        deposit.InvoiceUrl.ShouldBe("https://pay.example.com/1");
    }

    [Fact]
    public void MarkPaid_Should_Succeed_When_Pending()
    {
        var deposit = AppointmentDeposit.Create(Tenant, Appointment, Amount).Value;
        var paidAt = DateTimeOffset.UtcNow;

        var result = deposit.MarkPaid(paidAt);

        result.IsSuccess.ShouldBeTrue();
        deposit.Status.ShouldBe(AppointmentDepositStatus.Paid);
        deposit.PaidAtUtc.ShouldBe(paidAt);
    }

    [Fact]
    public void MarkPaid_Should_Be_Idempotent_When_Already_Paid()
    {
        var deposit = AppointmentDeposit.Create(Tenant, Appointment, Amount).Value;
        deposit.MarkPaid(DateTimeOffset.UtcNow);

        var result = deposit.MarkPaid(DateTimeOffset.UtcNow.AddMinutes(5));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void MarkFailed_Should_Fail_When_Already_Paid()
    {
        var deposit = AppointmentDeposit.Create(Tenant, Appointment, Amount).Value;
        deposit.MarkPaid(DateTimeOffset.UtcNow);

        var result = deposit.MarkFailed();

        result.IsFailure.ShouldBeTrue();
    }
}
