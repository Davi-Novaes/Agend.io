using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Marketing.Domain;

/// <summary>
/// Registro de uma campanha (e-mail ou WhatsApp) ja disparada — o envio e
/// sincrono do ponto de vista do dominio (SendCampaignCommandHandler ja
/// resolveu os destinatarios e persistiu isto antes de enfileirar os jobs
/// individuais), entao nao ha maquina de estado aqui: o registro so existe
/// depois de "enviado". Sem ISoftDeletable — e um log imutavel, nunca editado.
/// TargetSegment e string (nao o enum CustomerSegment de Customers.Contracts)
/// de proposito: Domain nunca referencia outro modulo, nem seu .Contracts —
/// so SharedKernel (ver CLAUDE.md). E um snapshot do nome do segmento no
/// momento do envio, null quando a campanha foi para todos os clientes ativos.
/// </summary>
public sealed class Campaign : AggregateRoot<CampaignId>, ITenantOwned, IAuditable
{
    public TenantId TenantId { get; private set; } = null!;

    public string Subject { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public CampaignChannel Channel { get; private set; }

    public string? TargetSegment { get; private set; }

    public int RecipientCount { get; private set; }

    public DateTimeOffset SentAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    private Campaign()
    {
    }

    private Campaign(
        TenantId tenantId, string subject, string body, CampaignChannel channel, string? targetSegment, int recipientCount,
        DateTimeOffset sentAtUtc)
        : base(CampaignId.New())
    {
        TenantId = tenantId;
        Subject = subject;
        Body = body;
        Channel = channel;
        TargetSegment = targetSegment;
        RecipientCount = recipientCount;
        SentAtUtc = sentAtUtc;
    }

    /// <summary>
    /// recipientCustomerIds so alimenta o CampaignSentDomainEvent (quem foi
    /// contatado, pra Customers marcar LastContactedAtUtc) — NAO e persistido
    /// no agregado (RecipientCount ja e o snapshot que importa aqui).
    /// </summary>
    public static Result<Campaign> Create(
        TenantId tenantId, string? subject, string? body, CampaignChannel channel, string? targetSegment,
        IReadOnlyList<Guid> recipientCustomerIds, DateTimeOffset sentAtUtc)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Result.Failure<Campaign>(Error.Validation("Campaign.SubjectRequired", "Informe o assunto da campanha."));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Result.Failure<Campaign>(Error.Validation("Campaign.BodyRequired", "Informe o texto da campanha."));
        }

        var campaign = new Campaign(tenantId, subject.Trim(), body.Trim(), channel, targetSegment, recipientCustomerIds.Count, sentAtUtc);
        campaign.Raise(new CampaignSentDomainEvent(campaign.Id, tenantId, recipientCustomerIds, sentAtUtc));
        return Result.Success(campaign);
    }
}
