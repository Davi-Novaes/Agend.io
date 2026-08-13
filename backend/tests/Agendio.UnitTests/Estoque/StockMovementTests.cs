using Agendio.Modules.Estoque.Domain;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.UnitTests.Estoque;

public class StockMovementTests
{
    private static readonly TenantId Tenant = TenantId.From(Guid.NewGuid());
    private static readonly ProductId Product = ProductId.New();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_Should_Fail_When_Quantity_Is_Zero_Or_Negative(int quantity)
    {
        var result = StockMovement.Create(
            Tenant, Product, StockMovementType.Entry, quantity, StockMovementReason.Purchase, null, DateTimeOffset.UtcNow);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Succeed_With_Valid_Data()
    {
        var occurredAtUtc = DateTimeOffset.UtcNow;

        var result = StockMovement.Create(
            Tenant, Product, StockMovementType.Exit, 3, StockMovementReason.Sale, "Venda avulsa", occurredAtUtc);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Quantity.ShouldBe(3);
        result.Value.Type.ShouldBe(StockMovementType.Exit);
        result.Value.Reason.ShouldBe(StockMovementReason.Sale);
        result.Value.Notes.ShouldBe("Venda avulsa");
        result.Value.OccurredAtUtc.ShouldBe(occurredAtUtc);
    }

    [Fact]
    public void Create_Should_Store_Null_Notes_When_Blank()
    {
        var result = StockMovement.Create(
            Tenant, Product, StockMovementType.Entry, 1, StockMovementReason.Adjustment, "   ", DateTimeOffset.UtcNow);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Notes.ShouldBeNull();
    }
}
