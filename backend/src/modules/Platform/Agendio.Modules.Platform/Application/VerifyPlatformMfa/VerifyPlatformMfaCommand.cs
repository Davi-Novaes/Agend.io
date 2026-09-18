using Agendio.Modules.Platform.Application.LoginPlatformAdmin;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Platform.Application.VerifyPlatformMfa;

/// <summary>TenantId nao existe aqui (PlatformAdmin fica fora de tenant) — challenge resolvido so por token, ver RedisPlatformMfaChallengeStore.</summary>
public sealed record VerifyPlatformMfaCommand(string ChallengeToken, string Code) : ICommand<LoginPlatformAdminResult>;
