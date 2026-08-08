namespace Agendio.Infrastructure.Security;

/// <summary>Criptografia simetrica para colunas sensiveis (CPF, dado de saude, segredo de MFA) — ver docs/adr/0007.</summary>
public interface IEncryptionService
{
    string Encrypt(string plaintext);

    string Decrypt(string ciphertext);
}
