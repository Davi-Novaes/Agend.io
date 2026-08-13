namespace Agendio.Modules.Tenancy.Domain;

/// <summary>
/// Fonte usada na pagina publica do estabelecimento. Lista curada (nao texto
/// livre) para nao expor a fragilidade de carregar uma fonte arbitraria do
/// Google Fonts em tempo de build.
/// </summary>
public enum PublicPageFont
{
    Default,
    Poppins,
    PlayfairDisplay,
    Merriweather,
}
