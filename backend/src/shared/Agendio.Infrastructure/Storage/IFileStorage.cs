namespace Agendio.Infrastructure.Storage;

/// <summary>
/// Armazenamento de arquivo minimo — disco local por enquanto (ver
/// LocalFileStorage). Quando o modulo de Storage completo existir (S3/MinIO,
/// Sprint futuro), essa troca acontece atras desta interface sem tocar quem a
/// consome (UpdateTenantLogoCommandHandler).
/// </summary>
public interface IFileStorage
{
    /// <summary>Salva o conteudo e devolve a URL publica (relativa) para servir o arquivo.</summary>
    Task<string> SaveAsync(string relativePath, Stream content, CancellationToken cancellationToken = default);
}
