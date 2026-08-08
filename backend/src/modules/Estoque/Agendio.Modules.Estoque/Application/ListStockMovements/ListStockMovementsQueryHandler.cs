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
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = dbContext.StockMovements.AsNoTracking().AsQueryable();

        if (request.ProductId is not null)
        {
            query = query.Where(m => m.ProductId == request.ProductId);
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

        var totalCount = await query.CountAsync(cancellationToken);

        var movements = await query
            .OrderByDescending(m => m.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Join em memoria de proposito: comparar StockMovement.ProductId (Guid
        // puro) com Product.Id (TypedId convertido) num join do EF nao traduz
        // para SQL. A pagina ja limitou o resultado — poucos produtos distintos.
        var productIds = movements.Select(m => ProductId.From(m.ProductId)).Distinct().ToList();
        var productNames = productIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id.Value, p => p.Name, cancellationToken);

        var items = movements
            .Select(m => new StockMovementSummary(
                m.Id.Value, m.ProductId, productNames.GetValueOrDefault(m.ProductId, "?"), m.Type, m.Quantity, m.Reason, m.Notes,
                m.OccurredAtUtc))
            .ToList();

        return Result.Success(new ListStockMovementsResult(items, totalCount, page, pageSize));
    }
}
