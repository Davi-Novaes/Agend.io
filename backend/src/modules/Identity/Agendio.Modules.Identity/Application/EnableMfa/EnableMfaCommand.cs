using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Identity.Application.EnableMfa;

/// <summary>
/// Secret vem do SetupMfaCommand anterior (nunca persistido ate aqui). Code
/// confirma que o usuario realmente configurou o app autenticador com ele antes
/// de MFA virar exigencia de login.
/// </summary>
public sealed record EnableMfaCommand(Guid UserId, string Secret, string Code) : ICommand<IReadOnlyList<string>>;
