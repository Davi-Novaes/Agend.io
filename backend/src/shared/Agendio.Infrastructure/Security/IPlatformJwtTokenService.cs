using System.Security.Claims;

namespace Agendio.Infrastructure.Security;

public interface IPlatformJwtTokenService
{
    (string Token, DateTimeOffset ExpiresAtUtc) GenerateAccessToken(IEnumerable<Claim> claims);
}
