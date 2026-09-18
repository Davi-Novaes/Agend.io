using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Platform.Application.SetupPlatformMfa;

/// <summary>AdminId vem da claim do JWT (admin ja autenticado) — ver PlatformEndpoints.GetAdminId.</summary>
public sealed record SetupPlatformMfaCommand(Guid AdminId) : ICommand<SetupPlatformMfaResult>;

public sealed record SetupPlatformMfaResult(string Secret, string OtpAuthUri);
