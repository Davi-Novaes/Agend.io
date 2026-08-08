namespace Agendio.Modules.Identity.Application.Login;

/// <summary>
/// Uniao simples (nao um tipo generico novo no SharedKernel) para os dois
/// desfechos possiveis de um login bem-sucedido: tokens de verdade, ou um
/// desafio de MFA pendente. Ver LoginCommandHandler.
/// </summary>
public abstract record LoginResult;

public sealed record LoginSuccess(AuthTokensResult Tokens) : LoginResult;

public sealed record LoginMfaChallenge(string ChallengeToken, DateTimeOffset ExpiresAtUtc) : LoginResult;
