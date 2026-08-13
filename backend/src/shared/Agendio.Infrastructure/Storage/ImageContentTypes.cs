namespace Agendio.Infrastructure.Storage;

/// <summary>Formatos de imagem aceitos em upload (logo/foto/imagem) em todo o produto — PNG/JPEG/WEBP.</summary>
public static class ImageContentTypes
{
    public static readonly IReadOnlyDictionary<string, string> ExtensionByContentType = new Dictionary<string, string>
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/webp"] = ".webp",
    };
}
