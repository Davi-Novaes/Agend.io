namespace Agendio.SharedKernel.Time;

/// <summary>
/// Abstrai o relogio para toda decisao de negocio sensivel a data/hora. Nunca usar
/// DateTimeOffset.UtcNow/DateTime.Now direto em Domain ou Application — sem essa
/// disciplina, testar "isso esta dentro do prazo de 24h" vira um teste instavel
/// que depende de que hora o CI decide rodar.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
