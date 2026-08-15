using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.Modules.Customers.Domain;

/// <summary>
/// Cliente de UM tenant (nao confundir com User — quem faz login no painel).
/// CustomData e o "campo customizado sem mudar schema": cada segmento guarda o
/// que precisa (convenio/alergia numa clinica, raca/porte num pet shop) sem
/// migration nova. Validacao de schema por tenant fica para uma iteracao
/// futura — por ora e so um dicionario livre, para nao overengineering antes
/// de existir um segundo consumidor real desse dado.
/// </summary>
public sealed class Customer : AggregateRoot<CustomerId>, ITenantOwned, IAuditable, ISoftDeletable
{
    public TenantId TenantId { get; private set; } = null!;

    public string FullName { get; private set; } = string.Empty;

    public Email? Email { get; private set; }

    public PhoneNumber? Phone { get; private set; }

    public string? Notes { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

    /// <summary>Criptografado em coluna (ver docs/adr/0007) — nunca aparece em log/auditoria (AuditLogInterceptor.SensitiveNameFragments).</summary>
    public string? Cpf { get; private set; }

    /// <summary>Dado de saude opcional (convenio, alergia, restricao) para segmentos como clinica/psicologo — criptografado em coluna, mesma justificativa de Cpf.</summary>
    public string? HealthNotes { get; private set; }

    public IReadOnlyDictionary<string, string> CustomData { get; private set; } = new Dictionary<string, string>();

    public bool IsActive { get; private set; }

    /// <summary>Saldo atual do programa de fidelidade (Fase 11) — a soma dos lancamentos em LoyaltyPointsLedgerEntry, mantida aqui como projecao para leitura rapida.</summary>
    public int LoyaltyPoints { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    private Customer()
    {
    }

    private Customer(
        TenantId tenantId, string fullName, Email? email, PhoneNumber? phone,
        string? notes, DateOnly? dateOfBirth, string? cpf, string? healthNotes,
        IReadOnlyDictionary<string, string> customData)
        : base(CustomerId.New())
    {
        TenantId = tenantId;
        FullName = fullName;
        Email = email;
        Phone = phone;
        Notes = notes;
        DateOfBirth = dateOfBirth;
        Cpf = cpf;
        HealthNotes = healthNotes;
        CustomData = customData;
        IsActive = true;
    }

    public static Result<Customer> Create(
        TenantId tenantId,
        string? fullName,
        string? email,
        string? phone,
        string? notes,
        DateOnly? dateOfBirth,
        IReadOnlyDictionary<string, string>? customData = null,
        string? cpf = null,
        string? healthNotes = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result.Failure<Customer>(Error.Validation("Customer.FullNameEmpty", "O nome do cliente nao pode ser vazio."));
        }

        var emailResult = ParseOptionalEmail(email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<Customer>(emailResult.Error);
        }

        var phoneResult = ParseOptionalPhone(phone);
        if (phoneResult.IsFailure)
        {
            return Result.Failure<Customer>(phoneResult.Error);
        }

        var cpfResult = ParseOptionalCpf(cpf);
        if (cpfResult.IsFailure)
        {
            return Result.Failure<Customer>(cpfResult.Error);
        }

        var customer = new Customer(
            tenantId, fullName.Trim(), emailResult.Value, phoneResult.Value, notes?.Trim(), dateOfBirth,
            cpfResult.Value, healthNotes?.Trim(), customData ?? new Dictionary<string, string>());

        customer.Raise(new CustomerCreatedDomainEvent(customer.Id, tenantId, customer.FullName));

        return Result.Success(customer);
    }

    public Result Update(
        string? fullName,
        string? email,
        string? phone,
        string? notes,
        DateOnly? dateOfBirth,
        IReadOnlyDictionary<string, string>? customData,
        string? cpf = null,
        string? healthNotes = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result.Failure(Error.Validation("Customer.FullNameEmpty", "O nome do cliente nao pode ser vazio."));
        }

        var emailResult = ParseOptionalEmail(email);
        if (emailResult.IsFailure)
        {
            return Result.Failure(emailResult.Error);
        }

        var phoneResult = ParseOptionalPhone(phone);
        if (phoneResult.IsFailure)
        {
            return Result.Failure(phoneResult.Error);
        }

        var cpfResult = ParseOptionalCpf(cpf);
        if (cpfResult.IsFailure)
        {
            return Result.Failure(cpfResult.Error);
        }

        FullName = fullName.Trim();
        Email = emailResult.Value;
        Phone = phoneResult.Value;
        Notes = notes?.Trim();
        DateOfBirth = dateOfBirth;
        Cpf = cpfResult.Value;
        HealthNotes = healthNotes?.Trim();
        CustomData = customData ?? CustomData;

        return Result.Success();
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public Result EarnLoyaltyPoints(int points)
    {
        if (points <= 0)
        {
            return Result.Failure(Error.Validation("Customer.InvalidLoyaltyPoints", "A quantidade de pontos deve ser maior que zero."));
        }

        LoyaltyPoints += points;
        return Result.Success();
    }

    public Result RedeemLoyaltyReward(int cost)
    {
        if (cost <= 0)
        {
            return Result.Failure(Error.Validation("Customer.InvalidLoyaltyPoints", "A quantidade de pontos deve ser maior que zero."));
        }

        if (LoyaltyPoints < cost)
        {
            return Result.Failure(Error.Validation("Customer.InsufficientLoyaltyPoints", "O cliente nao tem pontos suficientes para resgatar essa recompensa."));
        }

        LoyaltyPoints -= cost;
        return Result.Success();
    }

    private static Result<Email?> ParseOptionalEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Success<Email?>(null);
        }

        var result = Email.Create(email);
        return result.IsSuccess ? Result.Success<Email?>(result.Value) : Result.Failure<Email?>(result.Error);
    }

    private static Result<PhoneNumber?> ParseOptionalPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return Result.Success<PhoneNumber?>(null);
        }

        var result = PhoneNumber.Create(phone);
        return result.IsSuccess ? Result.Success<PhoneNumber?>(result.Value) : Result.Failure<PhoneNumber?>(result.Error);
    }

    // Cpf fica como string (digitos normalizados), nao CpfCnpj: quem persiste
    // e le e o valor criptografado em coluna (EncryptedStringConverter atua em
    // string?), a validacao de formato e so no momento de aceitar o valor.
    private static Result<string?> ParseOptionalCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
        {
            return Result.Success<string?>(null);
        }

        var result = CpfCnpj.Create(cpf);
        return result.IsSuccess ? Result.Success<string?>(result.Value.Value) : Result.Failure<string?>(result.Error);
    }
}
