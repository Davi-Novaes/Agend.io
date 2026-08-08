using Agendio.Infrastructure.Security;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Agendio.Infrastructure.Persistence;

/// <summary>
/// Conversor de coluna reutilizavel por qualquer modulo com campo sensivel
/// (CPF, dado de saude, segredo de MFA — ver docs/adr/0007). Nao pode ser
/// aplicado dentro de um IEntityTypeConfiguration (instanciado sem parametro
/// via ApplyConfigurationsFromAssembly) porque depende de IEncryptionService —
/// o DbContext do modulo recebe o servico via construtor e aplica isto direto
/// no OnModelCreating, depois de ApplyConfigurationsFromAssembly rodar.
/// </summary>
public sealed class EncryptedStringConverter : ValueConverter<string?, string?>
{
    public EncryptedStringConverter(IEncryptionService encryptionService)
        : base(
            plaintext => plaintext == null ? null : encryptionService.Encrypt(plaintext),
            ciphertext => ciphertext == null ? null : encryptionService.Decrypt(ciphertext))
    {
    }
}
