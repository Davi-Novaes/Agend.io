using Agendio.Modules.Financeiro.Domain;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.UnitTests.Financeiro;

public class AccountPayableTests
{
    private static readonly TenantId Tenant = TenantId.From(Guid.NewGuid());
    private static readonly Money Amount = Money.Create(80m).Value;
    private static readonly DateOnly DueDate = new(2026, 8, 10);

    [Fact]
    public void Create_Should_Fail_When_Description_Is_Empty()
    {
        var result = AccountPayable.Create(Tenant, "  ", Amount, DueDate, ExpenseCategory.Rent);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Start_As_Pending_With_No_Resource_Or_Appointment_For_Manual_Expense()
    {
        var result = AccountPayable.Create(Tenant, "Aluguel", Amount, DueDate, ExpenseCategory.Rent);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(AccountPayableStatus.Pending);
        result.Value.ResourceId.ShouldBeNull();
        result.Value.SourceAppointmentId.ShouldBeNull();
    }

    [Fact]
    public void Create_Should_Accept_ResourceId_And_SourceAppointmentId_For_Commission_Entries()
    {
        var resourceId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        var result = AccountPayable.Create(
            Tenant, "Comissao — Corte", Amount, DueDate, ExpenseCategory.Commission, resourceId, appointmentId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ResourceId.ShouldBe(resourceId);
        result.Value.SourceAppointmentId.ShouldBe(appointmentId);
    }

    [Fact]
    public void MarkPaid_Should_Succeed_When_Pending()
    {
        var payable = AccountPayable.Create(Tenant, "Aluguel", Amount, DueDate, ExpenseCategory.Rent).Value;
        var now = DateTimeOffset.UtcNow;

        var result = payable.MarkPaid(now);

        result.IsSuccess.ShouldBeTrue();
        payable.Status.ShouldBe(AccountPayableStatus.Paid);
        payable.PaidAtUtc.ShouldBe(now);
    }

    [Fact]
    public void MarkPaid_Should_Fail_When_Already_Cancelled()
    {
        var payable = AccountPayable.Create(Tenant, "Aluguel", Amount, DueDate, ExpenseCategory.Rent).Value;
        payable.Cancel();

        var result = payable.MarkPaid(DateTimeOffset.UtcNow);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Cancel_Should_Fail_When_Already_Paid()
    {
        var payable = AccountPayable.Create(Tenant, "Aluguel", Amount, DueDate, ExpenseCategory.Rent).Value;
        payable.MarkPaid(DateTimeOffset.UtcNow);

        var result = payable.Cancel();

        result.IsFailure.ShouldBeTrue();
    }
}
