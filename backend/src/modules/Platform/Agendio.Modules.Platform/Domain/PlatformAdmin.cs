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
}
