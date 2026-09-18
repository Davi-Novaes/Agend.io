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

    /// <summary>E.164 (ex.: +5511999998888). Null para membros de equipe convidados (so o dono/responsavel precisa, ver RegisterUserCommandValidator).</summary>
    public string? Phone { get; private set; }

    /// <summary>Digitos normalizados (nao criptografado com CpfCnpj — mesmo padrao de Customer.Cpf), criptografado em coluna. Null para membros de equipe convidados.</summary>
    public string? Cpf { get; private set; }

    /// <summary>Momento em que o dono/responsavel aceitou os Termos de Uso e a Politica de Privacidade — trilha de consentimento (LGPD). Null para membros de equipe convidados (quem aceita e o dono, uma vez, no cadastro do estabelecimento).</summary>
    public DateTimeOffset? TermsAcceptedAtUtc { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Foto de perfil, mesmo padrao de Resource.PhotoUrl/Service.ImageUrl — caminho relativo servido por IFileStorage, null enquanto ninguem fez upload.</summary>
    public string? AvatarUrl { get; private set; }

    public bool MfaEnabled { get; private set; }

    /// <summary>Segredo TOTP (base32) criptografado em coluna (ver docs/adr/0007) — null enquanto MFA nao esta habilitado.</summary>
    public string? MfaSecretEncrypted { get; private set; }

    /// <summary>Null enquanto o e-mail nao foi confirmado — bloqueia login (ver LoginCommandHandler).</summary>
    public DateTimeOffset? EmailConfirmedAt { get; private set; }

    /// <summary>Hash SHA-256 do token de confirmacao vigente — mesmo padrao de TeamInvitation.TokenHash. Null apos confirmado.</summary>
    public string? EmailConfirmationTokenHash { get; private set; }

    public DateTimeOffset? EmailConfirmationTokenExpiresAtUtc { get; private set; }

    public int FailedLoginAttemptCount { get; private set; }

    /// <summary>Null enquanto a conta nao esta bloqueada. Passado o instante, a conta volta a autenticar normalmente (ver IsLockedOut).</summary>
    public DateTimeOffset? LockedUntilUtc { get; private set; }

    /// <summary>
    /// Quantas vezes esta conta ja foi bloqueada seguidas (sem um login bem-sucedido
    /// ou reset de senha no meio) — determina a duracao do PROXIMO bloqueio em
    /// RegisterFailedLoginAttempt (1min -> 15min -> 30min). Ao contrario de
    /// FailedLoginAttemptCount, NAO zera so porque o bloqueio anterior expirou:
    /// e exatamente essa memoria entre ciclos que torna o bloqueio progressivo.
    /// </summary>
    public int LockoutEscalationLevel { get; private set; }

    /// <summary>Hash SHA-256 do token de recuperacao de senha vigente — mesmo padrao de EmailConfirmationTokenHash. Null quando nao ha pedido pendente.</summary>
    public string? PasswordResetTokenHash { get; private set; }

    public DateTimeOffset? PasswordResetTokenExpiresAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    private User()
    {
    }

    private User(
        TenantId tenantId, Email email, string fullName, string passwordHash, UserRole role, DateTimeOffset? emailConfirmedAtUtc,
        string? phone, string? cpf, DateTimeOffset? termsAcceptedAtUtc)
        : base(UserId.New())
    {
        TenantId = tenantId;
        Email = email;
        FullName = fullName;
        PasswordHash = passwordHash;
        Role = role;
        IsActive = true;
        EmailConfirmedAt = emailConfirmedAtUtc;
        Phone = phone;
        Cpf = cpf;
        TermsAcceptedAtUtc = termsAcceptedAtUtc;
    }

    /// <summary>
    /// <paramref name="emailConfirmedAtUtc"/>: null para autocadastro (precisa confirmar por
    /// e-mail antes do primeiro login, ver LoginCommandHandler). AcceptInvitationCommandHandler
    /// passa clock.UtcNow aqui — quem aceita um convite ja provou posse do e-mail ao receber o
    /// link, e-mail de confirmacao redundante so adicionaria friccao sem ganho de seguranca.
    ///
    /// <paramref name="phone"/>/<paramref name="cpf"/>/<paramref name="termsAcceptedAtUtc"/>:
    /// obrigatorios so pra quem se autocadastra como dono/responsavel — RegisterUserCommandValidator
    /// exige os tres; AcceptInvitationCommandHandler (membro de equipe convidado) nao passa
    /// nenhum, de proposito (quem ja aceitou os termos foi o dono, uma vez, no cadastro do
    /// estabelecimento — nao faz sentido pedir de novo a cada convite aceito).
    /// </summary>
    public static Result<User> Register(
        TenantId tenantId, Email email, string? fullName, string passwordHash, UserRole role = UserRole.Owner, DateTimeOffset? emailConfirmedAtUtc = null,
        string? phone = null, string? cpf = null, DateTimeOffset? termsAcceptedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result.Failure<User>(Error.Validation("User.FullNameEmpty", "O nome nao pode ser vazio."));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Result.Failure<User>(Error.Validation("User.PasswordHashEmpty", "Hash de senha invalido."));
        }

        var user = new User(tenantId, email, fullName.Trim(), passwordHash, role, emailConfirmedAtUtc, phone, cpf, termsAcceptedAtUtc);
        user.Raise(new UserRegisteredDomainEvent(user.Id, tenantId, email.Value));

        return Result.Success(user);
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    /// <summary>Autoatendimento — o usuario so edita o proprio nome/telefone (ver UpdateMyProfileCommandHandler, sem checagem de role). E-mail fica de fora de proposito: mudar exigiria um fluxo de confirmacao que nao existe ainda.</summary>
    public Result UpdateProfile(string? fullName, string? phone)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result.Failure(Error.Validation("User.FullNameEmpty", "O nome nao pode ser vazio."));
        }

        FullName = fullName.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();

        return Result.Success();
    }

    public Result SetAvatar(string avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            return Result.Failure(Error.Validation("User.InvalidAvatarUrl", "URL da foto invalida."));
        }

        AvatarUrl = avatarUrl;
        return Result.Success();
    }

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

    private const int MaxFailedLoginAttempts = 5;

    /// <summary>
    /// Bloqueio progressivo: 1o bloqueio (apos as primeiras 5 tentativas) dura
    /// 1 minuto, o 2o (mais 5 tentativas) 15 minutos, o 3o em diante (mais 5,
    /// e qualquer bloqueio depois disso) fica no teto de 30 minutos — pedido de
    /// produto pra deixar claro pro usuario que continuar errando piora, sem
    /// travar a conta pra sempre. Indexado por LockoutEscalationLevel (1-based).
    /// </summary>
    private static readonly TimeSpan[] LockoutDurationsByEscalationLevel =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30),
    ];

    public bool IsLockedOut(DateTimeOffset nowUtc) => LockedUntilUtc is { } lockedUntil && lockedUntil > nowUtc;

    /// <summary>
    /// Chamado a cada senha incorreta apresentada para esta conta (login e
    /// troca/recuperacao de senha nunca chamam isto, so falha de credencial no
    /// login). Se o bloqueio anterior ja expirou, reinicia a CONTAGEM DE
    /// TENTATIVAS do bloco atual antes de somar (nao deixa um bloqueio antigo
    /// "grudar" pra sempre) — mas LockoutEscalationLevel nao reseta aqui, de
    /// proposito: e ele quem lembra quantos bloqueios seguidos ja aconteceram
    /// entre um login bem-sucedido e outro. Ao atingir o limite do bloco,
    /// tranca a partir de AGORA pela duracao do proximo nivel de escalonamento
    /// (nao estende se ja estava trancada — RegisterFailedLoginAttempt so eh
    /// chamado quando IsLockedOut ja deu false no LoginCommandHandler, entao
    /// aqui a conta sempre esta destrancada no momento da chamada).
    /// </summary>
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
            FailedLoginAttemptCount = 0;
            LockoutEscalationLevel++;
            var durationIndex = Math.Min(LockoutEscalationLevel, LockoutDurationsByEscalationLevel.Length) - 1;
            LockedUntilUtc = nowUtc.Add(LockoutDurationsByEscalationLevel[durationIndex]);
        }
    }

    public void RegisterSuccessfulLogin()
    {
        FailedLoginAttemptCount = 0;
        LockedUntilUtc = null;
        LockoutEscalationLevel = 0;
    }

    public void GeneratePasswordResetToken(string tokenHash, DateTimeOffset expiresAtUtc)
    {
        PasswordResetTokenHash = tokenHash;
        PasswordResetTokenExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>Usado tanto por "esqueci minha senha" (token) quanto por troca autenticada (ChangePasswordCommandHandler ja validou a senha atual) — ambos os fluxos convergem aqui porque o efeito colateral (invalidar token pendente) e o mesmo.</summary>
    public Result ResetPassword(string newPasswordHash, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            return Result.Failure(Error.Validation("User.PasswordHashEmpty", "Hash de senha invalido."));
        }

        PasswordHash = newPasswordHash;
        PasswordResetTokenHash = null;
        PasswordResetTokenExpiresAtUtc = null;
        FailedLoginAttemptCount = 0;
        LockedUntilUtc = null;
        LockoutEscalationLevel = 0;

        return Result.Success();
    }

    public Result ConsumePasswordResetToken(DateTimeOffset nowUtc)
    {
        if (PasswordResetTokenHash is null || PasswordResetTokenExpiresAtUtc is null || PasswordResetTokenExpiresAtUtc <= nowUtc)
        {
            return Result.Failure(Error.Unauthorized("Auth.PasswordResetTokenInvalid", "Token de recuperacao invalido ou expirado."));
        }

        return Result.Success();
    }
}
