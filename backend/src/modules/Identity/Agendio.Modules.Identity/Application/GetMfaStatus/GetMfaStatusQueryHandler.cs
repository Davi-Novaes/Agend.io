using Agendio.Modules.Identity.Contracts;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Application.GetMfaStatus;

public sealed class GetMfaStatusQueryHandler(IdentityDbContext dbContext) : IQueryHandler<GetMfaStatusQuery, MfaStatusResult>
{
    public async Task<Result<MfaStatusResult>> Handle(GetMfaStatusQuery request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == UserId.From(request.UserId), cancellationToken);

        if (user is null)
        {
            return Result.Failure<MfaStatusResult>(Error.NotFound("User.NotFound", "Usuario nao encontrado."));
        }

        return Result.Success(new MfaStatusResult(user.MfaEnabled));
    }
}
