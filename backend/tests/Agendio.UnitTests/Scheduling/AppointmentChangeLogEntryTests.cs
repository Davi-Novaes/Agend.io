using Agendio.Modules.Scheduling.Domain;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.UnitTests.Scheduling;

public class AppointmentChangeLogEntryTests
{
    private static readonly TenantId Tenant = TenantId.From(Guid.NewGuid());
    private static readonly AppointmentId Appointment = AppointmentId.New();
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly Guid Resource = Guid.NewGuid();
    private static readonly DateTimeOffset PreviousStart = DateTimeOffset.UtcNow.AddDays(3);

    [Fact]
    public void RecordCancellation_Should_Succeed_Without_Reason()
    {
        var result = AppointmentChangeLogEntry.RecordCancellation(
            Tenant, Appointment, Customer, Resource, "Corte", PreviousStart, byStaff: true, reason: null, DateTimeOffset.UtcNow);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ChangeType.ShouldBe(AppointmentChangeType.Cancelled);
        result.Value.NewStartUtc.ShouldBeNull();
        result.Value.Reason.ShouldBeNull();
    }

    [Fact]
    public void RecordCancellation_Should_Trim_Reason()
    {
        var result = AppointmentChangeLogEntry.RecordCancellation(
            Tenant, Appointment, Customer, Resource, "Corte", PreviousStart, byStaff: false, reason: "  cliente desistiu  ", DateTimeOffset.UtcNow);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Reason.ShouldBe("cliente desistiu");
        result.Value.ByStaff.ShouldBeFalse();
    }

    [Fact]
    public void RecordCancellation_Should_Fail_When_Reason_Exceeds_Max_Length()
    {
        var longReason = new string('a', 501);

        var result = AppointmentChangeLogEntry.RecordCancellation(
            Tenant, Appointment, Customer, Resource, "Corte", PreviousStart, byStaff: true, longReason, DateTimeOffset.UtcNow);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void RecordReschedule_Should_Store_Previous_And_New_Start()
    {
        var newStart = PreviousStart.AddDays(2);

        var result = AppointmentChangeLogEntry.RecordReschedule(
            Tenant, Appointment, Customer, Resource, "Corte", PreviousStart, newStart, "cliente pediu outro dia", DateTimeOffset.UtcNow);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ChangeType.ShouldBe(AppointmentChangeType.Rescheduled);
        result.Value.PreviousStartUtc.ShouldBe(PreviousStart);
        result.Value.NewStartUtc.ShouldBe(newStart);
        result.Value.ByStaff.ShouldBeTrue();
    }

    [Fact]
    public void RecordReschedule_Should_Store_Previous_Resource_And_New_End_When_Provided()
    {
        var newStart = PreviousStart.AddDays(2);
        var newEnd = newStart.AddMinutes(45);
        var previousResource = Guid.NewGuid();

        var result = AppointmentChangeLogEntry.RecordReschedule(
            Tenant, Appointment, Customer, Resource, "Corte", PreviousStart, newStart, reason: null, DateTimeOffset.UtcNow,
            previousResourceId: previousResource, newEndUtc: newEnd);

        result.IsSuccess.ShouldBeTrue();
        result.Value.PreviousResourceId.ShouldBe(previousResource);
        result.Value.NewEndUtc.ShouldBe(newEnd);
    }

    [Fact]
    public void RecordReschedule_Should_Leave_Previous_Resource_And_New_End_Null_By_Default()
    {
        var result = AppointmentChangeLogEntry.RecordReschedule(
            Tenant, Appointment, Customer, Resource, "Corte", PreviousStart, PreviousStart.AddDays(1), reason: null, DateTimeOffset.UtcNow);

        result.IsSuccess.ShouldBeTrue();
        result.Value.PreviousResourceId.ShouldBeNull();
        result.Value.NewEndUtc.ShouldBeNull();
    }

    [Fact]
    public void RecordReschedule_Should_Fail_When_Reason_Exceeds_Max_Length()
    {
        var longReason = new string('a', 501);

        var result = AppointmentChangeLogEntry.RecordReschedule(
            Tenant, Appointment, Customer, Resource, "Corte", PreviousStart, PreviousStart.AddDays(1), longReason, DateTimeOffset.UtcNow);

        result.IsFailure.ShouldBeTrue();
    }
}
