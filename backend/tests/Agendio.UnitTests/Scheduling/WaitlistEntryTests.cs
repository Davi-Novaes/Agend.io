using Agendio.Modules.Scheduling.Domain;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.UnitTests.Scheduling;

public class WaitlistEntryTests
{
    private static readonly TenantId Tenant = TenantId.From(Guid.NewGuid());
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly Guid Service = Guid.NewGuid();
    private static readonly DateOnly PreferredDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);

    [Fact]
    public void Create_Should_Fail_When_ServiceName_Is_Blank()
    {
        var result = WaitlistEntry.Create(Tenant, Customer, null, Service, "   ", PreferredDate, null);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Succeed_With_No_Resource_Preference()
    {
        var result = WaitlistEntry.Create(Tenant, Customer, null, Service, "Corte", PreferredDate, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ResourceId.ShouldBeNull();
        result.Value.Status.ShouldBe(WaitlistStatus.Waiting);
    }

    [Fact]
    public void Create_Should_Trim_Notes()
    {
        var result = WaitlistEntry.Create(Tenant, Customer, null, Service, "Corte", PreferredDate, "  qualquer horario a tarde  ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Notes.ShouldBe("qualquer horario a tarde");
    }

    [Fact]
    public void Create_Should_Fail_When_Notes_Exceed_Max_Length()
    {
        var longNotes = new string('a', 501);

        var result = WaitlistEntry.Create(Tenant, Customer, null, Service, "Corte", PreferredDate, longNotes);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void MarkNotified_Should_Transition_From_Waiting_To_Notified()
    {
        var entry = WaitlistEntry.Create(Tenant, Customer, null, Service, "Corte", PreferredDate, null).Value;
        var now = DateTimeOffset.UtcNow;

        var result = entry.MarkNotified(now);

        result.IsSuccess.ShouldBeTrue();
        entry.Status.ShouldBe(WaitlistStatus.Notified);
        entry.NotifiedAtUtc.ShouldBe(now);
    }

    [Fact]
    public void MarkNotified_Should_Fail_When_Already_Notified()
    {
        var entry = WaitlistEntry.Create(Tenant, Customer, null, Service, "Corte", PreferredDate, null).Value;
        entry.MarkNotified(DateTimeOffset.UtcNow);

        var result = entry.MarkNotified(DateTimeOffset.UtcNow);

        result.IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Convert_Should_Succeed_From_Waiting_Or_Notified(bool notifyFirst)
    {
        var entry = WaitlistEntry.Create(Tenant, Customer, null, Service, "Corte", PreferredDate, null).Value;
        if (notifyFirst)
        {
            entry.MarkNotified(DateTimeOffset.UtcNow);
        }

        var appointmentId = AppointmentId.New();
        var result = entry.Convert(appointmentId);

        result.IsSuccess.ShouldBeTrue();
        entry.Status.ShouldBe(WaitlistStatus.Converted);
        entry.ConvertedAppointmentId.ShouldBe(appointmentId);
    }

    [Fact]
    public void Convert_Should_Fail_When_Already_Converted()
    {
        var entry = WaitlistEntry.Create(Tenant, Customer, null, Service, "Corte", PreferredDate, null).Value;
        entry.Convert(AppointmentId.New());

        var result = entry.Convert(AppointmentId.New());

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Cancel_Should_Succeed_From_Waiting()
    {
        var entry = WaitlistEntry.Create(Tenant, Customer, null, Service, "Corte", PreferredDate, null).Value;

        var result = entry.Cancel();

        result.IsSuccess.ShouldBeTrue();
        entry.Status.ShouldBe(WaitlistStatus.Cancelled);
    }

    [Fact]
    public void Cancel_Should_Fail_When_Already_Converted()
    {
        var entry = WaitlistEntry.Create(Tenant, Customer, null, Service, "Corte", PreferredDate, null).Value;
        entry.Convert(AppointmentId.New());

        var result = entry.Cancel();

        result.IsFailure.ShouldBeTrue();
    }
}
