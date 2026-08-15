using Agendio.Modules.Estoque.Domain;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.UnitTests.Estoque;

public class ProductTests
{
    private static readonly TenantId Tenant = TenantId.From(Guid.NewGuid());

    [Fact]
    public void Create_Should_Fail_When_Name_Is_Empty()
    {
        var result = Product.Create(Tenant, "   ", null, null, null, 10, 2, null, null);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Fail_When_QuantityInStock_Is_Negative()
    {
        var result = Product.Create(Tenant, "Xampu", null, null, null, -1, 2, null, null);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Fail_When_MinimumStock_Is_Negative()
    {
        var result = Product.Create(Tenant, "Xampu", null, null, null, 10, -1, null, null);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Succeed_With_Valid_Data()
    {
        var result = Product.Create(
            Tenant, "Xampu", "SKU-1", "Cosmeticos", "Xampu revenda", 10, 2, Money.Create(12.5m).Value, Money.Create(29.9m).Value);

        result.IsSuccess.ShouldBeTrue();
        result.Value.QuantityInStock.ShouldBe(10);
        result.Value.Category.ShouldBe("Cosmeticos");
        result.Value.CostPrice!.Amount.ShouldBe(12.5m);
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Update_Should_Not_Change_QuantityInStock()
    {
        var product = Product.Create(Tenant, "Xampu", null, null, null, 10, 2, null, null).Value;

        var result = product.Update("Xampu 500ml", "SKU-2", "Cosmeticos", "Atualizado", 5, Money.Create(13m).Value, null);

        result.IsSuccess.ShouldBeTrue();
        product.QuantityInStock.ShouldBe(10);
        product.MinimumStock.ShouldBe(5);
        product.Category.ShouldBe("Cosmeticos");
        product.CostPrice!.Amount.ShouldBe(13m);
    }

    [Fact]
    public void Update_Should_Fail_When_Name_Is_Empty()
    {
        var product = Product.Create(Tenant, "Xampu", null, null, null, 10, 2, null, null).Value;

        var result = product.Update("  ", null, null, null, 2, null, null);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void ApplyMovement_Entry_Should_Increase_QuantityInStock()
    {
        var product = Product.Create(Tenant, "Xampu", null, null, null, 10, 2, null, null).Value;

        var result = product.ApplyMovement(StockMovementType.Entry, 5);

        result.IsSuccess.ShouldBeTrue();
        product.QuantityInStock.ShouldBe(15);
    }

    [Fact]
    public void ApplyMovement_Exit_Should_Decrease_QuantityInStock()
    {
        var product = Product.Create(Tenant, "Xampu", null, null, null, 10, 2, null, null).Value;

        var result = product.ApplyMovement(StockMovementType.Exit, 4);

        result.IsSuccess.ShouldBeTrue();
        product.QuantityInStock.ShouldBe(6);
    }

    [Fact]
    public void ApplyMovement_Exit_Should_Fail_When_Resulting_Quantity_Would_Be_Negative()
    {
        var product = Product.Create(Tenant, "Xampu", null, null, null, 10, 2, null, null).Value;

        var result = product.ApplyMovement(StockMovementType.Exit, 11);

        result.IsFailure.ShouldBeTrue();
        product.QuantityInStock.ShouldBe(10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ApplyMovement_Should_Fail_When_Quantity_Is_Zero_Or_Negative(int quantity)
    {
        var product = Product.Create(Tenant, "Xampu", null, null, null, 10, 2, null, null).Value;

        var result = product.ApplyMovement(StockMovementType.Entry, quantity);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void ApplyMovement_Should_Succeed_Even_When_Product_Is_Inactive()
    {
        var product = Product.Create(Tenant, "Xampu", null, null, null, 10, 2, null, null).Value;
        product.Deactivate();

        var result = product.ApplyMovement(StockMovementType.Exit, 3);

        result.IsSuccess.ShouldBeTrue();
        product.QuantityInStock.ShouldBe(7);
    }

    [Fact]
    public void Activate_Deactivate_Should_Toggle_IsActive()
    {
        var product = Product.Create(Tenant, "Xampu", null, null, null, 10, 2, null, null).Value;

        product.Deactivate();
        product.IsActive.ShouldBeFalse();

        product.Activate();
        product.IsActive.ShouldBeTrue();
    }
}
