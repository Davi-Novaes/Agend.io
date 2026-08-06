using System.Security.Claims;

namespace Agendio.Infrastructure.Security;

public interface IJwtTokenService
{
    (string Token, DateTimeOffset ExpiresAtUtc) GenerateAccessToken(IEnumerable<Claim> claims);
}
