using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Identity.Application.ResetPassword;

public sealed record ResetPasswordCommand(string Token, string NewPassword) : ICommand;
