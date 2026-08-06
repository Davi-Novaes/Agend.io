namespace Agendio.Infrastructure.Security;

/// <summary>Mapeia a secao "Jwt" da configuracao. A SigningKey vem de secrets/Key Vault, nunca de appsettings.json commitado.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    /// <summary>Chave simetrica HMAC-SHA256; precisa ter pelo menos 256 bits (32 bytes) de entropia.</summary>
    public required string SigningKey { get; init; }

    /// <summary>Curto de proposito: o access token vive so em memoria no frontend e nunca deveria durar muito.</summary>
    public int AccessTokenLifetimeMinutes { get; init; } = 15;

    public int RefreshTokenLifetimeDays { get; init; } = 30;
}
