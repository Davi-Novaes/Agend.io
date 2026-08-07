using Agendio.Modules.Customers.Domain;
using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Customers.Application.GetCustomerAuditLog;

/// <summary>
/// Trilha de auditoria de um cliente especifico — a resposta direta a um pedido
/// LGPD de "quais dados meus voces guardam e quem mexeu neles". audit_log nao e
/// ITenantOwned (nao tem Global Query Filter, ver AuditLogEntry), entao o filtro
/// de tenant aqui e MANUAL e obrigatorio; a segunda camada de defesa e a Row
/// Level Security da propria tabela, que barra qualquer linha de outro tenant
/// mesmo que este filtro seja esquecido no futuro.
/// </summary>
public sealed class GetCustomerAuditLogQueryHandler(CustomersDbContext dbContext, ITenantContext tenantContext)
    : IQueryHandler<GetCustomerAuditLogQuery, GetCustomerAuditLogResult>
{
    public async Task<Result<GetCustomerAuditLogResult>> Handle(GetCustomerAuditLogQuery request, CancellationToken cancellationToken)
    {
        var customerExists = await dbContext.Customers
            .AsNoTracking()
            .AnyAsync(c => c.Id == CustomerId.From(request.CustomerId), cancellationToken);

        if (!customerExists)
        {
            return Result.Failure<GetCustomerAuditLogResult>(Error.NotFound("Customer.NotFound", "Cliente nao encontrado."));
        }

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = dbContext.AuditLogEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantContext.TenantId.Value
                && e.EntityType == nameof(Customer)
                && e.EntityId == request.CustomerId);

        var totalCount = await query.CountAsync(cancellationToken);

        var entries = await query
            .OrderByDescending(e => e.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AuditLogEntrySummary(e.Id, e.Action, e.Before, e.After, e.PerformedBy, e.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success(new GetCustomerAuditLogResult(entries, totalCount, page, pageSize));
    }
}
