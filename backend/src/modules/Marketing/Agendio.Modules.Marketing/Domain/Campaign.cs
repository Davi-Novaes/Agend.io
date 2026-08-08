using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Marketing.Domain;

/// <summary>
/// Registro de uma campanha de e-mail ja disparada — o envio e sincrono do
/// ponto de vista do dominio (SendCampaignCommandHandler ja resolveu os
/// destinatarios e persistiu isto antes de enfileirar os jobs de e-mail
/// individuais), entao nao ha maquina de estado aqui: o registro so existe
/// depois de "enviado". Sem ISoftDeletable — e um log imutavel, nunca editado.
/// </summary>
public sealed class Campaign : AggregateRoot<CampaignId>, ITenantOwned, IAuditable
{
    public TenantId TenantId { get; private set; } = null!;

    public string Subject { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public int RecipientCount { get; private set; }

    public DateTimeOffset SentAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    private Campaign()
    {
    }

    private Campaign(TenantId tenantId, string subject, string body, int recipientCount, DateTimeOffset sentAtUtc)
        : base(CampaignId.New())
    {
        TenantId = tenantId;
        Subject = subject;
        Body = body;
        RecipientCount = recipientCount;
        SentAtUtc = sentAtUtc;
    }

    public static Result<Campaign> Create(TenantId tenantId, string? subject, string? body, int recipientCount, DateTimeOffset sentAtUtc)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Result.Failure<Campaign>(Error.Validation("Campaign.SubjectRequired", "Informe o assunto da campanha."));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Result.Failure<Campaign>(Error.Validation("Campaign.BodyRequired", "Informe o texto da campanha."));
        }

        if (recipientCount < 0)
        {
            return Result.Failure<Campaign>(Error.Validation("Campaign.InvalidRecipientCount", "Quantidade de destinatarios invalida."));
        }

        return Result.Success(new Campaign(tenantId, subject.Trim(), body.Trim(), recipientCount, sentAtUtc));
    }
}
