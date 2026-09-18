using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Identity.Application.ChangePassword;

public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : ICommand;
