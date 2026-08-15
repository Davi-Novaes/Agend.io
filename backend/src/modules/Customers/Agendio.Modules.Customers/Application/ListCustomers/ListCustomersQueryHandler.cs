using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.Modules.Scheduling.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Customers.Application.ListCustomers;

public sealed class ListCustomersQueryHandler(CustomersDbContext dbContext, ICustomerVisitStatsLookupService visitStatsLookup, IClock clock)
    : IQueryHandler<ListCustomersQuery, ListCustomersResult>
{
    public async Task<Result<ListCustomersResult>> Handle(ListCustomersQuery request, CancellationToken cancellationToken)
    {
        // O Global Query Filter ja restringe isto ao tenant do JWT do chamador.
        var query = dbContext.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            // So por nome de proposito: Email/Phone sao Value Objects mapeados
            // via HasConversion, e o EF nao traduz busca parcial (Contains/ILike)
            // dentro de um valor convertido — so igualdade do objeto inteiro (ver
            // comentario identico em CreateTenantCommandHandler).
            query = query.Where(c => EF.Functions.ILike(c.FullName, $"%{search}%"));
        }

        var allStats = await visitStatsLookup.ListAllAsync(cancellationToken);
        var statsByCustomerId = allStats.ToDictionary(s => s.CustomerId);
        var vipSpendThreshold = CustomerSegmentCalculator.CalculateVipSpendThreshold(
            allStats.Where(s => s.TotalVisits > 0).ToList());
        var nowUtc = clock.UtcNow;

        CustomerSegment SegmentFor(Guid customerId) =>
            CustomerSegmentCalculator.Calculate(statsByCustomerId.GetValueOrDefault(customerId), nowUtc, vipSpendThreshold);

        // Segmento e calculado na leitura, nao existe coluna pra filtrar no banco
        // — com filtro de segmento, materializa tudo que bate na busca e pagina
        // em memoria (escala pequena assumida, mesmo raciocinio de
        // GetInventorySummary/NotificationLogEntry). Sem filtro, mantem o caminho
        // rapido de paginacao no banco de hoje, so anexando o segmento por linha.
        if (request.Segment is { } segment)
        {
            var matching = await query.OrderBy(c => c.FullName).ToListAsync(cancellationToken);
            var filtered = matching.Where(c => SegmentFor(c.Id.Value) == segment).ToList();

            var page = Math.Max(request.Page, 1);
            var pageSize = Math.Clamp(request.PageSize, 1, PaginationExtensions.MaxPageSize);
            var pageItems = filtered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CustomerSummary(c.Id.Value, c.FullName, c.Email?.Value, c.Phone?.Value, c.IsActive, c.CreatedAtUtc, SegmentFor(c.Id.Value)))
                .ToList();

            return Result.Success(new ListCustomersResult(pageItems, filtered.Count, page, pageSize));
        }

        var paged = await query.OrderBy(c => c.FullName).ToPagedItemsAsync(request.Page, request.PageSize, cancellationToken);

        var items = paged.Items
            .Select(c => new CustomerSummary(c.Id.Value, c.FullName, c.Email?.Value, c.Phone?.Value, c.IsActive, c.CreatedAtUtc, SegmentFor(c.Id.Value)))
            .ToList();

        return Result.Success(new ListCustomersResult(items, paged.TotalCount, paged.Page, paged.PageSize));
    }
}
