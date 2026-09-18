namespace Agendio.Modules.Tenancy.Domain;

/// <summary>
/// Siglas de UF validas -- usado pra validar o campo Estado de Unit sem
/// depender de uma tabela/pacote externo so pra isso.
/// </summary>
public static class BrazilianStates
{
    public static readonly IReadOnlySet<string> Codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO",
        "MA", "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI",
        "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
    };
}
