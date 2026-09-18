using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Platform.Domain;

/// <summary>
/// Autoridade da plataforma — nunca um papel dentro de um tenant (ver CLAUDE.md:
/// "Super Admin e uma autoridade separada, nunca um papel dentro de tenant").
/// Nao implementa ITenantOwned de proposito: assim como Tenant, PlatformAdmin
/// esta FORA de qualquer tenant, nao pertence a um.
///
/// Email fica como string simples (nao um Value Object dedicado, ao contrario de
/// Identity.Domain.Email): o conjunto de admins da plataforma e pequeno e
/// controlado por deploy, nao um formulario publico recebendo entrada arbitraria
/// em escala — duplicar o Value Object de Identity aqui seria complexidade sem
/// beneficio real. Validacao de formato fica no Validator do comando de login/
/// criacao, e o uniqueness fica garantido por indice unico no banco.
/// </summary>
public sealed class PlatformAdmin : AggregateRoot<PlatformAdminId>, IAuditable
{
    public string Email { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    /// <summary>Hash Argon2id — mesmo IPasswordHasher usado por Identity (infraestrutura compartilhada, nao logica de tenant).</summary>
    public string PasswordHash { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public bool MfaEnabled { get; private set; }

    /// <summary>Segredo TOTP (base32) criptografado em coluna, mesmo padrao de Identity.Domain.User — ver PlatformDbContext.</summary>
    public string? MfaSecretEncrypted { get; private set; }

    public int FailedLoginAttemptCount { get; private set; }

    public DateTimeOffset? LockedUntilUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    private PlatformAdmin()
    {
    }

    private PlatformAdmin(string email, string fullName, string passwordHash) : base(PlatformAdminId.New())
    {
        Email = email;
        FullName = fullName;
        PasswordHash = passwordHash;
        IsActive = true;
    }

    public static Result<PlatformAdmin> Create(string? email, string? fullName, string? passwordHash)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure<PlatformAdmin>(Error.Validation("PlatformAdmin.EmailEmpty", "O e-mail nao pode ser vazio."));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result.Failure<PlatformAdmin>(Error.Validation("PlatformAdmin.FullNameEmpty", "O nome nao pode ser vazio."));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Result.Failure<PlatformAdmin>(Error.Validation("PlatformAdmin.PasswordHashEmpty", "Hash de senha invalido."));
        }

        return Result.Success(new PlatformAdmin(email.Trim().ToLowerInvariant(), fullName.Trim(), passwordHash));
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void EnableMfa(string secret)
    {
        MfaSecretEncrypted = secret;
        MfaEnabled = true;
    }

    // Sem codigo de recuperacao de proposito (ao contrario de Identity.Domain.User):
    // o conjunto de admins da plataforma e pequeno e controlado por deploy (ver
    // comentario da classe) — um TOTP perdido se resolve com acesso direto ao
    // banco por um operador, nao vale a complexidade extra de codigos de
    // recuperacao para uma superficie tao pequena. Reavaliar se o numero de
    // admins crescer.
    public void DisableMfa()
    {
        MfaSecretEncrypted = null;
        MfaEnabled = false;
    }

    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public bool IsLockedOut(DateTimeOffset nowUtc) => LockedUntilUtc is { } lockedUntil && lockedUntil > nowUtc;

    /// <summary>Mesma logica de Identity.Domain.User.RegisterFailedLoginAttempt — ver o comentario la para o raciocinio completo.</summary>
    public void RegisterFailedLoginAttempt(DateTimeOffset nowUtc)
    {
        if (LockedUntilUtc is { } previousLock && previousLock <= nowUtc)
        {
            FailedLoginAttemptCount = 0;
            LockedUntilUtc = null;
        }

        FailedLoginAttemptCount++;

        if (FailedLoginAttemptCount >= MaxFailedLoginAttempts)
        {
            LockedUntilUtc = nowUtc.Add(LockoutDuration);
        }
    }

    public void RegisterSuccessfulLogin()
    {
        FailedLoginAttemptCount = 0;
        LockedUntilUtc = null;
    }
}
