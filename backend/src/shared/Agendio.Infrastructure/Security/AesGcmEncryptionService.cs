using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Agendio.Infrastructure.Security;

/// <summary>
/// AES-256-GCM autenticado. Formato do valor armazenado: base64(nonce[12] +
/// tag[16] + ciphertext) — nonce novo a cada chamada de Encrypt, entao o mesmo
/// texto claro produz ciphertext diferente a cada vez (coluna criptografada
/// NAO e filtravel/ordenavel em SQL por causa disso — ver docs/adr/0007).
/// </summary>
public sealed class AesGcmEncryptionService : IEncryptionService
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly byte[] _key;

    public AesGcmEncryptionService(IOptions<ColumnEncryptionOptions> options)
    {
        _key = Convert.FromBase64String(options.Value.Key);

        if (_key.Length != 32)
        {
            throw new InvalidOperationException(
                $"ColumnEncryption:Key precisa decodificar para 32 bytes (AES-256), mas tem {_key.Length}.");
        }
    }

    public string Encrypt(string plaintext)
    {
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        using (var aesGcm = new AesGcm(_key, TagSizeBytes))
        {
            aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        }

        var payload = new byte[NonceSizeBytes + TagSizeBytes + ciphertext.Length];
        nonce.CopyTo(payload, 0);
        tag.CopyTo(payload, NonceSizeBytes);
        ciphertext.CopyTo(payload, NonceSizeBytes + TagSizeBytes);

        return Convert.ToBase64String(payload);
    }

    public string Decrypt(string ciphertext)
    {
        var payload = Convert.FromBase64String(ciphertext);

        var nonce = payload.AsSpan(0, NonceSizeBytes);
        var tag = payload.AsSpan(NonceSizeBytes, TagSizeBytes);
        var cipherBytes = payload.AsSpan(NonceSizeBytes + TagSizeBytes);

        var plaintextBytes = new byte[cipherBytes.Length];

        using (var aesGcm = new AesGcm(_key, TagSizeBytes))
        {
            aesGcm.Decrypt(nonce, cipherBytes, tag, plaintextBytes);
        }

        return System.Text.Encoding.UTF8.GetString(plaintextBytes);
    }
}
