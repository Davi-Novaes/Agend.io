using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Scheduling.Application.SubmitReview;

/// <summary>TenantId vem da rota publica — ver IHasExplicitTenant.</summary>
public sealed record SubmitReviewCommand(Guid TenantId, Guid AppointmentId, string CustomerEmail, int Rating, string? Comment)
    : ICommand, IHasExplicitTenant;
