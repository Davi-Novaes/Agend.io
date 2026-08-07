using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Customers.Application.ListCustomers;

public sealed class ListCustomersQueryHandler(CustomersDbContext dbContext) : IQueryHandler<ListCustomersQuery, ListCustomersResult>
{
    public async Task<Result<ListCustomersResult>> Handle(ListCustomersQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

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

        var totalCount = await query.CountAsync(cancellationToken);

        var customers = await query
            .OrderBy(c => c.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = customers
            .Select(c => new CustomerSummary(c.Id.Value, c.FullName, c.Email?.Value, c.Phone?.Value, c.IsActive, c.CreatedAtUtc))
            .ToList();

        return Result.Success(new ListCustomersResult(items, totalCount, page, pageSize));
    }
}
