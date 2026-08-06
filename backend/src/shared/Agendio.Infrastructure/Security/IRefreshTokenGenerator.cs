namespace Agendio.Infrastructure.Security;

/// <summary>
/// Refresh token e um segredo de alta entropia (nao uma senha escolhida por
/// humano), entao SHA-256 e suficiente para o hash de armazenamento — Argon2
/// existe para resistir a forca bruta contra senhas curtas/previsiveis, o que
/// nao e o caso aqui. So o hash e persistido; o token em texto plano so existe
/// no momento em que e entregue ao cliente.
/// </summary>
public interface IRefreshTokenGenerator
{
    string GenerateToken();

    string Hash(string token);
}
