using Agendio.Modules.Scheduling.Domain;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.UnitTests.Scheduling;

public class AppointmentTests
{
    private static readonly TenantId Tenant = TenantId.From(Guid.NewGuid());

    private static Appointment CreateAppointment(Guid resourceId, Guid? unitId = null)
    {
        var slot = TimeSlot.Create(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddMinutes(30)).Value;
        var price = Money.Create(45.90m).Value;
        return Appointment.Schedule(Tenant, Guid.NewGuid(), resourceId, unitId, Guid.NewGuid(), "Corte", slot, price, notes: null).Value;
    }

    [Fact]
    public void Reschedule_Should_Change_Only_The_Slot_When_Resource_Is_Not_Provided()
    {
        var originalResourceId = Guid.NewGuid();
        var originalUnitId = Guid.NewGuid();
        var appointment = CreateAppointment(originalResourceId, originalUnitId);
        var newSlot = TimeSlot.Create(DateTimeOffset.UtcNow.AddDays(2), DateTimeOffset.UtcNow.AddDays(2).AddMinutes(45)).Value;

        var result = appointment.Reschedule(newSlot);

        result.IsSuccess.ShouldBeTrue();
        appointment.Slot.ShouldBe(newSlot);
        appointment.ResourceId.ShouldBe(originalResourceId);
        appointment.UnitId.ShouldBe(originalUnitId);
    }

    [Fact]
    public void Reschedule_Should_Change_Resource_And_Unit_When_Provided()
    {
        var originalResourceId = Guid.NewGuid();
        var appointment = CreateAppointment(originalResourceId, Guid.NewGuid());
        var newSlot = TimeSlot.Create(DateTimeOffset.UtcNow.AddDays(2), DateTimeOffset.UtcNow.AddDays(2).AddMinutes(30)).Value;
        var newResourceId = Guid.NewGuid();
        var newUnitId = Guid.NewGuid();

        var result = appointment.Reschedule(newSlot, newResourceId, newUnitId);

        result.IsSuccess.ShouldBeTrue();
        appointment.ResourceId.ShouldBe(newResourceId);
        appointment.UnitId.ShouldBe(newUnitId);
    }

    [Fact]
    public void Reschedule_Should_Fail_When_Appointment_Is_Not_Scheduled_Or_Confirmed()
    {
        var appointment = CreateAppointment(Guid.NewGuid());
        appointment.Confirm();
        appointment.Start();
        appointment.Complete(DateTimeOffset.UtcNow);
        var newSlot = TimeSlot.Create(DateTimeOffset.UtcNow.AddDays(2), DateTimeOffset.UtcNow.AddDays(2).AddMinutes(30)).Value;

        var result = appointment.Reschedule(newSlot);

        result.IsFailure.ShouldBeTrue();
    }
}
