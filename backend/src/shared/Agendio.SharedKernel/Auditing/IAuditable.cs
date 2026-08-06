namespace Agendio.SharedKernel.Auditing;

/// <summary>
/// Entidade que registra quem criou/alterou e quando. Os setters existem para o
/// interceptor de auditoria do EF (Agendio.Infrastructure) preencher
/// automaticamente no SaveChanges — o codigo de dominio nunca escreve nestes campos.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAtUtc { get; set; }

    string? CreatedBy { get; set; }

    DateTimeOffset? UpdatedAtUtc { get; set; }

    string? UpdatedBy { get; set; }
}
