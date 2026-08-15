using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Scheduling.Domain;

/// <summary>
/// Avaliacao do cliente sobre um atendimento concluido (Fase 12) — imutavel,
/// nunca editada/apagada (mesmo raciocinio de NotificationLogEntry/StockMovement),
/// no maximo uma por agendamento (indice unico em AppointmentId, ver Configuration).
/// CustomerId/ResourceId sao Guid cru (cross-modulo, mesmo padrao de Appointment).
/// ServiceName e snapshot do momento do atendimento, nao muda se o servico for
/// renomeado depois. Desenhada como entidade independente de proposito, para uma
/// futura sincronizacao com avaliacoes do Google poder reaproveitar o schema sem
/// mudanca estrutural — sem nenhum campo especulativo adicionado agora.
/// </summary>
public sealed class Review : AggregateRoot<ReviewId>, ITenantOwned, IAuditable
{
    public TenantId TenantId { get; private set; } = null!;

    public AppointmentId AppointmentId { get; private set; } = null!;

    public Guid CustomerId { get; private set; }

    public Guid ResourceId { get; private set; }

    public string ServiceName { get; private set; } = string.Empty;

    public int Rating { get; private set; }

    public string? Comment { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    private Review()
    {
    }

    private Review(
        TenantId tenantId, AppointmentId appointmentId, Guid customerId, Guid resourceId, string serviceName, int rating, string? comment)
        : base(ReviewId.New())
    {
        TenantId = tenantId;
        AppointmentId = appointmentId;
        CustomerId = customerId;
        ResourceId = resourceId;
        ServiceName = serviceName;
        Rating = rating;
        Comment = comment;
    }

    public static Result<Review> Create(
        TenantId tenantId, AppointmentId appointmentId, Guid customerId, Guid resourceId, string serviceName, int rating, string? comment)
    {
        if (rating is < 1 or > 5)
        {
            return Result.Failure<Review>(Error.Validation("Review.InvalidRating", "A avaliacao deve ser de 1 a 5 estrelas."));
        }

        var trimmedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (trimmedComment is { Length: > 1000 })
        {
            return Result.Failure<Review>(Error.Validation("Review.CommentTooLong", "O comentario pode ter no maximo 1000 caracteres."));
        }

        return Result.Success(new Review(tenantId, appointmentId, customerId, resourceId, serviceName, rating, trimmedComment));
    }
}
