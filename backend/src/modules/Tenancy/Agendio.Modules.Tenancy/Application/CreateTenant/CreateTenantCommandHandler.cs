using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.CreateTenant;

public sealed class CreateTenantCommandHandler(TenancyDbContext dbContext) : ICommandHandler<CreateTenantCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        var slugResult = Slug.Create(request.Slug);
        if (slugResult.IsFailure)
        {
            return Result.Failure<Guid>(slugResult.Error);
        }

        // Compara o Value Object inteiro (nao ".Slug.Value") de proposito: o EF
        // so consegue traduzir esta consulta para SQL aplicando a mesma
        // ValueConverter configurada em TentantConfiguration ao objeto inteiro.
        // Acessar ".Value" dentro do predicado quebra a traducao (LINQ ->
        // client evaluation), pois o EF nao "olha dentro" de um tipo convertido.
        var slugAlreadyTaken = await dbContext.Tenants
            .AnyAsync(t => t.Slug == slugResult.Value, cancellationToken);

        if (slugAlreadyTaken)
        {
            return Result.Failure<Guid>(Error.Conflict("Tenant.SlugTaken", "Ja existe um estabelecimento com este identificador."));
        }

        var tenantResult = Domain.Tenant.Create(request.Name, request.Slug, request.BusinessType, request.TimeZoneId);
        if (tenantResult.IsFailure)
        {
            return Result.Failure<Guid>(tenantResult.Error);
        }

        dbContext.Tenants.Add(tenantResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(tenantResult.Value.Id.Value);
    }
}
