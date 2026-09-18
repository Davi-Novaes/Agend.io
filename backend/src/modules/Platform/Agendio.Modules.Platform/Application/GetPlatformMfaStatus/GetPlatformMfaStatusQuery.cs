using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Platform.Application.GetPlatformMfaStatus;

public sealed record GetPlatformMfaStatusQuery(Guid AdminId) : IQuery<PlatformMfaStatusResult>;

public sealed record PlatformMfaStatusResult(bool MfaEnabled);
