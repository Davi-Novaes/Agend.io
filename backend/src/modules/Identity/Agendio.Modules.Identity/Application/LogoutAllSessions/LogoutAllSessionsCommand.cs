using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Identity.Application.LogoutAllSessions;

/// <summary>UserId vem da claim do JWT (usuario ja autenticado) — ver IdentityEndpoints.GetUserId.</summary>
public sealed record LogoutAllSessionsCommand(Guid UserId) : ICommand;
