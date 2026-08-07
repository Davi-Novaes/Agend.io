using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Agendio.SharedKernel.Time;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Agendio.Infrastructure.Security;

/// <summary>
/// Espelha JwtTokenService de proposito, em vez de generalizar as duas atras de
/// um parametro comum: as duas autoridades (tenant e plataforma) tem que
/// permanecer trilhas de codigo totalmente independentes, para que um bug de
/// generalizacao nunca vire um jeito de uma emitir token da outra.
/// </summary>
public sealed class PlatformJwtTokenService(IOptions<PlatformJwtOptions> options, IClock clock) : IPlatformJwtTokenService
{
    private readonly PlatformJwtOptions _options = options.Value;

    public (string Token, DateTimeOffset ExpiresAtUtc) GenerateAccessToken(IEnumerable<Claim> claims)
    {
        var expiresAtUtc = clock.UtcNow.AddMinutes(_options.AccessTokenLifetimeMinutes);

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: clock.UtcNow.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);
        return (tokenValue, expiresAtUtc);
    }
}
