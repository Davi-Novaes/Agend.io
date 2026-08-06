using Isopoh.Cryptography.Argon2;

namespace Agendio.Infrastructure.Security;

/// <summary>
/// Argon2id (HybridAddressing) — vencedor da Password Hashing Competition e
/// recomendacao atual da OWASP, resistente tanto a ataque por GPU/ASIC quanto a
/// side-channel. Nunca trocar para MD5/SHA/bcrypt/PBKDF2 aqui: e regra de
/// seguranca do projeto (ver CLAUDE.md), nao preferencia de estilo.
///
/// Parametros seguem a recomendacao minima da OWASP para Argon2id (m=19MiB seria
/// o piso absoluto; usamos 64MiB por termos servidor dedicado, nao FaaS com
/// memoria restrita) — ajustar exige medir o impacto real no tempo de login.
/// </summary>
public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const int TimeCost = 3;
    private const int MemoryCostKib = 65536; // 64 MiB
    private const int Parallelism = 2;
    private const int HashLength = 32;

    public string Hash(string password) =>
        Argon2.Hash(
            password: password,
            timeCost: TimeCost,
            memoryCost: MemoryCostKib,
            parallelism: Parallelism,
            type: Argon2Type.HybridAddressing,
            hashLength: HashLength);

    public bool Verify(string password, string hash) => Argon2.Verify(hash, password);
}
