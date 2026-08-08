using Agendio.Modules.Estoque.Domain;
using Agendio.Modules.Estoque.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Estoque.Application.SetProductActiveStatus;

public sealed class SetProductActiveStatusCommandHandler(EstoqueDbContext dbContext) : ICommandHandler<SetProductActiveStatusCommand>
{
    public async Task<Result> Handle(SetProductActiveStatusCommand request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(p => p.Id == ProductId.From(request.ProductId), cancellationToken);
        if (product is null)
        {
            return Result.Failure(Error.NotFound("Product.NotFound", "Produto nao encontrado."));
        }

        if (request.IsActive)
        {
            product.Activate();
        }
        else
        {
            product.Deactivate();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
