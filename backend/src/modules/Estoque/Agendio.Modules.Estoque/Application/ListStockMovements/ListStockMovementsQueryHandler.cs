using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Estoque.Domain;
using Agendio.Modules.Estoque.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Estoque.Application.ListStockMovements;

public sealed class ListStockMovementsQueryHandler(EstoqueDbContext dbContext)
    : IQueryHandler<ListStockMovementsQuery, ListStockMovementsResult>
{
    public async Task<Result<ListStockMovementsResult>> Handle(ListStockMovementsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.StockMovements.AsNoTracking().AsQueryable();

        if (request.ProductId is { } productId)
        {
            var typedProductId = ProductId.From(productId);
            query = query.Where(m => m.ProductId == typedProductId);
        }

        if (request.Type is not null)
        {
            query = query.Where(m => m.Type == request.Type);
        }

        if (request.Reason is not null)
        {
            query = query.Where(m => m.Reason == request.Reason);
        }

        if (request.From is not null)
        {
            var fromUtc = new DateTimeOffset(request.From.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(m => m.OccurredAtUtc >= fromUtc);
        }

        if (request.To is not null)
        {
            var toUtcExclusive = new DateTimeOffset(request.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(m => m.OccurredAtUtc < toUtcExclusive);
        }

        var paged = await query.OrderByDescending(m => m.OccurredAtUtc).ToPagedItemsAsync(request.Page, request.PageSize, cancellationToken);

        // Segunda consulta em vez de join: a pagina ja limitou o resultado —
        // poucos produtos distintos — e evita repetir o nome do produto na
        // projeção principal.
        var productIds = paged.Items.Select(m => m.ProductId).Distinct().ToList();
        var productNames = productIds.Count == 0
            ? new Dictionary<ProductId, string>()
            : await dbContext.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var items = paged.Items
            .Select(m => new StockMovementSummary(
                m.Id.Value, m.ProductId.Value, productNames.GetValueOrDefault(m.ProductId, "?"), m.Type, m.Quantity, m.Reason, m.Notes,
                m.OccurredAtUtc))
            .ToList();

        return Result.Success(new ListStockMovementsResult(items, paged.TotalCount, paged.Page, paged.PageSize));
    }
}
