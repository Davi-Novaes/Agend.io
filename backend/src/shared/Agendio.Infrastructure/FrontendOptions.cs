namespace Agendio.Infrastructure;

/// <summary>Usado para montar links absolutos (convite, recuperacao de senha) enviados por e-mail.</summary>
public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    public required string BaseUrl { get; init; }
}
