using Agendio.Modules.Financeiro.Domain;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.UnitTests.Financeiro;

public class AccountReceivableTests
{
    private static readonly TenantId Tenant = TenantId.From(Guid.NewGuid());
    private static readonly Money Amount = Money.Create(150m).Value;
    private static readonly DateOnly DueDate = new(2026, 8, 10);

    [Fact]
    public void Create_Should_Fail_When_Description_Is_Empty()
    {
        var result = AccountReceivable.Create(Tenant, "   ", Amount, DueDate);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Start_As_Pending()
    {
        var result = AccountReceivable.Create(Tenant, "Corte de cabelo", Amount, DueDate);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(AccountReceivableStatus.Pending);
        result.Value.ReceivedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void MarkReceived_Should_Succeed_When_Pending()
    {
        var receivable = AccountReceivable.Create(Tenant, "Corte de cabelo", Amount, DueDate).Value;
        var now = DateTimeOffset.UtcNow;

        var result = receivable.MarkReceived(now);

        result.IsSuccess.ShouldBeTrue();
        receivable.Status.ShouldBe(AccountReceivableStatus.Received);
        receivable.ReceivedAtUtc.ShouldBe(now);
    }

    [Fact]
    public void MarkReceived_Should_Fail_When_Already_Received()
    {
        var receivable = AccountReceivable.Create(Tenant, "Corte de cabelo", Amount, DueDate).Value;
        receivable.MarkReceived(DateTimeOffset.UtcNow);

        var result = receivable.MarkReceived(DateTimeOffset.UtcNow);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Cancel_Should_Succeed_When_Pending()
    {
        var receivable = AccountReceivable.Create(Tenant, "Corte de cabelo", Amount, DueDate).Value;

        var result = receivable.Cancel();

        result.IsSuccess.ShouldBeTrue();
        receivable.Status.ShouldBe(AccountReceivableStatus.Cancelled);
    }

    [Fact]
    public void Cancel_Should_Fail_When_Already_Received()
    {
        var receivable = AccountReceivable.Create(Tenant, "Corte de cabelo", Amount, DueDate).Value;
        receivable.MarkReceived(DateTimeOffset.UtcNow);

        var result = receivable.Cancel();

        result.IsFailure.ShouldBeTrue();
    }
}
