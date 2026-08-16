using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Identity.Application.ConfirmEmail;

public sealed record ConfirmEmailCommand(string Token) : ICommand;
