using Agendio.Modules.Identity.Contracts;
using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.Modules.Identity.Domain;

public sealed class User : AggregateRoot<UserId>, ITenantOwned, IAuditable, ISoftDeletable
{
    public TenantId TenantId { get; private set; } = null!;

    public Email Email { get; private set; } = null!;

    public string FullName { get; private set; } = string.Empty;

    /// <summary>Hash Argon2id — nunca a senha em texto plano (ver IPasswordHasher em Agendio.Infrastructure).</summary>
    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public bool MfaEnabled { get; private set; }

    /// <summary>Segredo TOTP (base32) criptografado em coluna (ver docs/adr/0007) — null enquanto MFA nao esta habilitado.</summary>
    public string? MfaSecretEncrypted { get; private set; }

    /// <summary>Null enquanto o e-mail nao foi confirmado — bloqueia login (ver LoginCommandHandler).</summary>
    public DateTimeOffset? EmailConfirmedAt { get; private set; }

    /// <summary>Hash SHA-256 do token de confirmacao vigente — mesmo padrao de TeamInvitation.TokenHash. Null apos confirmado.</summary>
    public string? EmailConfirmationTokenHash { get; private set; }

    public DateTimeOffset? EmailConfirmationTokenExpiresAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    private User()
    {
    }

    private User(TenantId tenantId, Email email, string fullName, string passwordHash, UserRole role, DateTimeOffset? emailConfirmedAtUtc)
        : base(UserId.New())
    {
        TenantId = tenantId;
        Email = email;
        FullName = fullName;
        PasswordHash = passwordHash;
        Role = role;
        IsActive = true;
        EmailConfirmedAt = emailConfirmedAtUtc;
    }

    /// <summary>
    /// <paramref name="emailConfirmedAtUtc"/>: null para autocadastro (precisa confirmar por
    /// e-mail antes do primeiro login, ver LoginCommandHandler). AcceptInvitationCommandHandler
    /// passa clock.UtcNow aqui — quem aceita um convite ja provou posse do e-mail ao receber o
    /// link, e-mail de confirmacao redundante so adicionaria friccao sem ganho de seguranca.
    /// </summary>
    public static Result<User> Register(
        TenantId tenantId, Email email, string? fullName, string passwordHash, UserRole role = UserRole.Owner, DateTimeOffset? emailConfirmedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result.Failure<User>(Error.Validation("User.FullNameEmpty", "O nome nao pode ser vazio."));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Result.Failure<User>(Error.Validation("User.PasswordHashEmpty", "Hash de senha invalido."));
        }

        var user = new User(tenantId, email, fullName.Trim(), passwordHash, role, emailConfirmedAtUtc);
        user.Raise(new UserRegisteredDomainEvent(user.Id, tenantId, email.Value));

        return Result.Success(user);
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void GenerateEmailConfirmationToken(string tokenHash, DateTimeOffset expiresAtUtc)
    {
        EmailConfirmationTokenHash = tokenHash;
        EmailConfirmationTokenExpiresAtUtc = expiresAtUtc;
    }

    public Result ConfirmEmail(DateTimeOffset nowUtc)
    {
        if (EmailConfirmedAt is not null)
        {
            return Result.Success();
        }

        if (EmailConfirmationTokenHash is null || EmailConfirmationTokenExpiresAtUtc is null || EmailConfirmationTokenExpiresAtUtc <= nowUtc)
        {
            return Result.Failure(Error.Unauthorized("Auth.EmailConfirmationTokenInvalid", "Token de confirmacao invalido ou expirado."));
        }

        EmailConfirmedAt = nowUtc;
        EmailConfirmationTokenHash = null;
        EmailConfirmationTokenExpiresAtUtc = null;

        return Result.Success();
    }

    public void EnableMfa(string secret)
    {
        MfaSecretEncrypted = secret;
        MfaEnabled = true;
    }

    public void DisableMfa()
    {
        MfaSecretEncrypted = null;
        MfaEnabled = false;
    }
}
