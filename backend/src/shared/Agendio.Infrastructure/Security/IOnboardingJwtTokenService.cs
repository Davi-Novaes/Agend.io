using System.Security.Claims;

namespace Agendio.Infrastructure.Security;

public interface IOnboardingJwtTokenService
{
    (string Token, DateTimeOffset ExpiresAtUtc) GenerateAccessToken(IEnumerable<Claim> claims);
}
