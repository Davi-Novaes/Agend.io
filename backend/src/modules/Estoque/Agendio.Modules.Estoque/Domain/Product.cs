using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.Modules.Estoque.Domain;

/// <summary>
/// Um produto revendido ao cliente (ex.: xampu numa barbearia). QuantityInStock
/// so muda via ApplyMovement — nunca diretamente por Update, para garantir que
/// toda alteracao de saldo deixa um StockMovement correspondente.
/// </summary>
public sealed class Product : AggregateRoot<ProductId>, ITenantOwned, IAuditable, ISoftDeletable
{
    public TenantId TenantId { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public string? Sku { get; private set; }

    public string? Category { get; private set; }

    public string? Description { get; private set; }

    public int QuantityInStock { get; private set; }

    public int MinimumStock { get; private set; }

    public Money? CostPrice { get; private set; }

    public Money? SalePrice { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    private Product()
    {
    }

    private Product(
        TenantId tenantId, string name, string? sku, string? category, string? description, int quantityInStock, int minimumStock,
        Money? costPrice, Money? salePrice)
        : base(ProductId.New())
    {
        TenantId = tenantId;
        Name = name;
        Sku = sku;
        Category = category;
        Description = description;
        QuantityInStock = quantityInStock;
        MinimumStock = minimumStock;
        CostPrice = costPrice;
        SalePrice = salePrice;
        IsActive = true;
    }

    public static Result<Product> Create(
        TenantId tenantId, string? name, string? sku, string? category, string? description, int quantityInStock, int minimumStock,
        Money? costPrice, Money? salePrice)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Product>(Error.Validation("Product.NameEmpty", "O nome nao pode ser vazio."));
        }

        if (quantityInStock < 0)
        {
            return Result.Failure<Product>(Error.Validation("Product.NegativeQuantity", "A quantidade em estoque nao pode ser negativa."));
        }

        if (minimumStock < 0)
        {
            return Result.Failure<Product>(Error.Validation("Product.NegativeMinimumStock", "O estoque minimo nao pode ser negativo."));
        }

        return Result.Success(new Product(
            tenantId, name.Trim(), NullIfBlank(sku), NullIfBlank(category), NullIfBlank(description), quantityInStock, minimumStock,
            costPrice, salePrice));
    }

    public Result Update(string? name, string? sku, string? category, string? description, int minimumStock, Money? costPrice, Money? salePrice)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(Error.Validation("Product.NameEmpty", "O nome nao pode ser vazio."));
        }

        if (minimumStock < 0)
        {
            return Result.Failure(Error.Validation("Product.NegativeMinimumStock", "O estoque minimo nao pode ser negativo."));
        }

        Name = name.Trim();
        Sku = NullIfBlank(sku);
        Category = NullIfBlank(category);
        Description = NullIfBlank(description);
        MinimumStock = minimumStock;
        CostPrice = costPrice;
        SalePrice = salePrice;
        return Result.Success();
    }

    public Result ApplyMovement(StockMovementType type, int quantity)
    {
        if (quantity <= 0)
        {
            return Result.Failure(Error.Validation("Product.InvalidQuantity", "A quantidade da movimentacao precisa ser maior que zero."));
        }

        if (type == StockMovementType.Exit && quantity > QuantityInStock)
        {
            return Result.Failure(Error.Validation("Product.InsufficientStock", "Estoque insuficiente para essa saida."));
        }

        QuantityInStock += type == StockMovementType.Entry ? quantity : -quantity;
        return Result.Success();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
