using Agendio.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Agendio.UnitTests.Infrastructure;

/// <summary>
/// Base do requisito "dado sensivel (CPF, saude) criptografado em coluna" do
/// CLAUDE.md — ver docs/adr/0007. Chave fixa de 32 bytes so para o teste, nao
/// tem relacao com nenhuma chave real de dev/producao.
/// </summary>
public class AesGcmEncryptionServiceTests
{
    private const string ValidKey = "aW50ZWdyYXRpb24tdGVzdC1jb2wta2V5LTMyYnl0ZXM=";

    [Fact]
    public void Encrypt_Then_Decrypt_Should_Roundtrip_The_Plaintext()
    {
        var service = CreateService(ValidKey);

        var ciphertext = service.Encrypt("529.982.247-25");
        var plaintext = service.Decrypt(ciphertext);

        plaintext.ShouldBe("529.982.247-25");
    }

    [Fact]
    public void Encrypt_Should_Produce_Different_Ciphertext_For_The_Same_Plaintext_Twice()
    {
        var service = CreateService(ValidKey);

        var first = service.Encrypt("mesmo texto");
        var second = service.Encrypt("mesmo texto");

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Decrypt_Should_Throw_When_Ciphertext_Was_Tampered_With()
    {
        var service = CreateService(ValidKey);
        var ciphertext = service.Encrypt("texto sensivel");

        var tamperedBytes = Convert.FromBase64String(ciphertext);
        tamperedBytes[^1] ^= 0xFF;
        var tampered = Convert.ToBase64String(tamperedBytes);

        Should.Throw<System.Security.Cryptography.AuthenticationTagMismatchException>(() => service.Decrypt(tampered));
    }

    [Fact]
    public void Constructor_Should_Throw_When_Key_Is_Not_32_Bytes()
    {
        Should.Throw<InvalidOperationException>(() => CreateService(Convert.ToBase64String("chave-curta-demais"u8.ToArray())));
    }

    private static AesGcmEncryptionService CreateService(string key) =>
        new(Options.Create(new ColumnEncryptionOptions { Key = key }));
}
