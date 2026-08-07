using Microsoft.AspNetCore.Hosting;

namespace Agendio.Infrastructure.Storage;

public sealed class LocalFileStorage(IWebHostEnvironment environment) : IFileStorage
{
    private const string PublicRequestPath = "/uploads";
    private const string UploadsFolderName = "uploads";

    public async Task<string> SaveAsync(string relativePath, Stream content, CancellationToken cancellationToken = default)
    {
        var uploadsRoot = Path.Combine(environment.ContentRootPath, UploadsFolderName);
        var fullPath = Path.Combine(uploadsRoot, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using (var fileStream = File.Create(fullPath))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        // Barra normal sempre, mesmo no Windows: isto vira parte de uma URL.
        return $"{PublicRequestPath}/{relativePath.Replace('\\', '/')}";
    }
}
