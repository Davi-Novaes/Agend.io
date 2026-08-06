using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Identity.Application.AcceptInvitation;

public sealed record AcceptInvitationCommand(string Token, string FullName, string Password) : ICommand<Guid>;
