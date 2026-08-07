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

    public IReadOnlyDictionary<string, string> CustomData { get; private set; } = new Dictionary<string, string>();

    public bool IsActive { get; private set; }

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
        string? notes, DateOnly? dateOfBirth, IReadOnlyDictionary<string, string> customData)
        : base(CustomerId.New())
    {
        TenantId = tenantId;
        FullName = fullName;
        Email = email;
        Phone = phone;
        Notes = notes;
        DateOfBirth = dateOfBirth;
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
        IReadOnlyDictionary<string, string>? customData = null)
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

        var customer = new Customer(
            tenantId, fullName.Trim(), emailResult.Value, phoneResult.Value, notes?.Trim(), dateOfBirth,
            customData ?? new Dictionary<string, string>());

        customer.Raise(new CustomerCreatedDomainEvent(customer.Id, tenantId, customer.FullName));

        return Result.Success(customer);
    }

    public Result Update(
        string? fullName,
        string? email,
        string? phone,
        string? notes,
        DateOnly? dateOfBirth,
        IReadOnlyDictionary<string, string>? customData)
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

        FullName = fullName.Trim();
        Email = emailResult.Value;
        Phone = phoneResult.Value;
        Notes = notes?.Trim();
        DateOfBirth = dateOfBirth;
        CustomData = customData ?? CustomData;

        return Result.Success();
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

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
}
