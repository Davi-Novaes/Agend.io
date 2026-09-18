using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Platform.Application.EnablePlatformMfa;

/// <summary>Secret vem do SetupPlatformMfaCommand anterior (nunca persistido ate aqui). Code confirma que o admin configurou o app autenticador antes de MFA virar exigencia de login.</summary>
public sealed record EnablePlatformMfaCommand(Guid AdminId, string Secret, string Code) : ICommand;
