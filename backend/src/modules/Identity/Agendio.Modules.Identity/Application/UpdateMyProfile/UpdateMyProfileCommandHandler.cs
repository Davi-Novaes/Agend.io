using Agendio.Modules.Identity.Contracts;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Application.UpdateMyProfile;

public sealed class UpdateMyProfileCommandHandler(IdentityDbContext dbContext) : ICommandHandler<UpdateMyProfileCommand>
{
    public async Task<Result> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Id == UserId.From(request.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("User.NotFound", "Usuario nao encontrado."));
        }

        var updateResult = user.UpdateProfile(request.FullName, request.Phone);
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
