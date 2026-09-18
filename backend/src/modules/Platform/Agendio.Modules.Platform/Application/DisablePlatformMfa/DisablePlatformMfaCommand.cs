using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Platform.Application.DisablePlatformMfa;

/// <summary>Senha + codigo TOTP: duas provas de identidade para desligar o segundo fator.</summary>
public sealed record DisablePlatformMfaCommand(Guid AdminId, string Password, string Code) : ICommand;
