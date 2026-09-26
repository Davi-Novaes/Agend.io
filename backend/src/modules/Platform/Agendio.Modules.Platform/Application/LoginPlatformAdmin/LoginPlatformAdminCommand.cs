using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Security;

namespace Agendio.Modules.Platform.Application.LoginPlatformAdmin;

public sealed record LoginPlatformAdminCommand(string Email, string Password, string TurnstileToken)
    : ICommand<LoginPlatformAdminOutcome>, IRequiresTurnstileVerification;

public sealed record LoginPlatformAdminResult(string AccessToken, DateTimeOffset ExpiresAtUtc, string FullName);

/// <summary>Mesma uniao de LoginResult (Identity): tokens de verdade, ou um desafio de MFA pendente. Ver LoginPlatformAdminCommandHandler.</summary>
public abstract record LoginPlatformAdminOutcome;

public sealed record LoginPlatformAdminSuccess(LoginPlatformAdminResult Result) : LoginPlatformAdminOutcome;

public sealed record LoginPlatformAdminMfaChallenge(string ChallengeToken, DateTimeOffset ExpiresAtUtc) : LoginPlatformAdminOutcome;
