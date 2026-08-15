namespace Agendio.Modules.Customers.Contracts;

/// <summary>Fase 9 — auto-segmentacao, calculado na leitura (nunca persistido em Customer).</summary>
public enum CustomerSegment
{
    Novo,
    Recorrente,
    Vip,
    EmRisco,
    Inativo,
    NoShow,
}
