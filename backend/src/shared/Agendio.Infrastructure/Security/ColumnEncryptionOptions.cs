namespace Agendio.Infrastructure.Security;

/// <summary>
/// Mapeia a secao "ColumnEncryption" da configuracao. Mesmo padrao de
/// JwtOptions/AsaasOptions: chave em appsettings.Development.json em dev,
/// esperada via variavel de ambiente/secret store em producao — nao existe
/// Key Vault neste projeto ainda (ver docs/adr/0007).
/// </summary>
public sealed class ColumnEncryptionOptions
{
    public const string SectionName = "ColumnEncryption";

    /// <summary>Base64 de 32 bytes (AES-256). AesGcmEncryptionService lanca na inicializacao se o tamanho nao bater.</summary>
    public required string Key { get; init; }
}
