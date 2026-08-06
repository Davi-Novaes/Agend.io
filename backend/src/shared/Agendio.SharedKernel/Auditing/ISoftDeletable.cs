namespace Agendio.SharedKernel.Auditing;

/// <summary>
/// Entidade que e desativada em vez de fisicamente apagada — necessario para
/// manter historico (agendamentos passados, notas fiscais) mesmo apos "exclusao".
/// O global query filter do EF esconde registros com IsDeleted = true por padrao.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }

    DateTimeOffset? DeletedAtUtc { get; set; }
}
