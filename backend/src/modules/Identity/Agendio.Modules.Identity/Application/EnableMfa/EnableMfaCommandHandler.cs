using System.Security.Cryptography;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Contracts;
using Agendio.Modules.Identity.Domain;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using OtpNet;

namespace Agendio.Modules.Identity.Application.EnableMfa;

public sealed class EnableMfaCommandHandler(IdentityDbContext dbContext, IRefreshTokenGenerator codeGenerator, IClock clock)
    : ICommandHandler<EnableMfaCommand, IReadOnlyList<string>>
{
    private const int RecoveryCodeCount = 10;
    private static readonly VerificationWindow TotpWindow = new(previous: 1, future: 1);

    // Sem I/O/O confundivel: sem 0/O/1/I. Formato XXXX-XXXX, digitavel a mao.
    private const string RecoveryCodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public async Task<Result<IReadOnlyList<string>>> Handle(EnableMfaCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Id == UserId.From(request.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure<IReadOnlyList<string>>(Error.NotFound("User.NotFound", "Usuario nao encontrado."));
        }

        if (user.MfaEnabled)
        {
            return Result.Failure<IReadOnlyList<string>>(Error.Validation("Mfa.AlreadyEnabled", "MFA ja esta habilitado para este usuario."));
        }

        var totp = new Totp(Base32Encoding.ToBytes(request.Secret));
        if (!totp.VerifyTotp(request.Code, out _, TotpWindow))
        {
            return Result.Failure<IReadOnlyList<string>>(Error.Validation("Mfa.InvalidCode", "Codigo invalido."));
        }

        user.EnableMfa(request.Secret);

        // Unica vez que os codigos aparecem em texto puro — so o hash e persistido.
        var plaintextCodes = new List<string>(RecoveryCodeCount);
        for (var i = 0; i < RecoveryCodeCount; i++)
        {
            var rawCode = GenerateRecoveryCode();
            plaintextCodes.Add(rawCode);

            var codeHash = codeGenerator.Hash(rawCode);
            dbContext.MfaRecoveryCodes.Add(MfaRecoveryCode.Create(user.TenantId, user.Id, codeHash, clock.UtcNow));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success<IReadOnlyList<string>>(plaintextCodes);
    }

    private static string GenerateRecoveryCode()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(8);
        Span<char> buffer = stackalloc char[9];

        for (var i = 0; i < 4; i++)
        {
            buffer[i] = RecoveryCodeAlphabet[randomBytes[i] % RecoveryCodeAlphabet.Length];
        }

        buffer[4] = '-';

        for (var i = 0; i < 4; i++)
        {
            buffer[5 + i] = RecoveryCodeAlphabet[randomBytes[4 + i] % RecoveryCodeAlphabet.Length];
        }

        return new string(buffer);
    }
}
