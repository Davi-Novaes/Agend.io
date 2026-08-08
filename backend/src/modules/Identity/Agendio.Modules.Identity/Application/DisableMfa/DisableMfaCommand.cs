using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Identity.Application.DisableMfa;

/// <summary>Senha + codigo (TOTP ou recuperacao): duas provas de identidade para desligar o segundo fator.</summary>
public sealed record DisableMfaCommand(Guid UserId, string Password, string Code) : ICommand;
