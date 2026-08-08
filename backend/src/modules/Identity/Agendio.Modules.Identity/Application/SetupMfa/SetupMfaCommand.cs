using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Identity.Application.SetupMfa;

/// <summary>UserId vem da claim do JWT (usuario ja autenticado) — ver IdentityEndpoints.GetUserId.</summary>
public sealed record SetupMfaCommand(Guid UserId) : ICommand<SetupMfaResult>;

public sealed record SetupMfaResult(string Secret, string OtpAuthUri);
