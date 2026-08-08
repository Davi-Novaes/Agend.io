using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Contracts;
using Agendio.Modules.Identity.Infrastructure.Mfa;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Application.DisableMfa;

public sealed class DisableMfaCommandHandler(IdentityDbContext dbContext, IPasswordHasher passwordHasher, IMfaCodeVerifier mfaCodeVerifier)
    : ICommandHandler<DisableMfaCommand>
{
    public async Task<Result> Handle(DisableMfaCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Id == UserId.From(request.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("User.NotFound", "Usuario nao encontrado."));
        }

        if (!user.MfaEnabled)
        {
            return Result.Failure(Error.Validation("Mfa.NotEnabled", "MFA nao esta habilitado para este usuario."));
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Result.Failure(Error.Unauthorized("Auth.InvalidCredentials", "Senha invalida."));
        }

        if (!await mfaCodeVerifier.VerifyAsync(user, request.Code, cancellationToken))
        {
            return Result.Failure(Error.Unauthorized("Auth.InvalidMfaCode", "Codigo invalido."));
        }

        user.DisableMfa();

        var recoveryCodes = await dbContext.MfaRecoveryCodes.Where(rc => rc.UserId == user.Id).ToListAsync(cancellationToken);
        dbContext.MfaRecoveryCodes.RemoveRange(recoveryCodes);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
