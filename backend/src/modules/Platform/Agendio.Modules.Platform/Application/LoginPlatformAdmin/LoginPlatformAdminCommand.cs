using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Platform.Application.LoginPlatformAdmin;

public sealed record LoginPlatformAdminCommand(string Email, string Password) : ICommand<LoginPlatformAdminResult>;

public sealed record LoginPlatformAdminResult(string AccessToken, DateTimeOffset ExpiresAtUtc, string FullName);
