using System.Security.Cryptography;
using System.Text;

namespace Agendio.Infrastructure.Security;

public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    private const int TokenSizeBytes = 64;

    public string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenSizeBytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
