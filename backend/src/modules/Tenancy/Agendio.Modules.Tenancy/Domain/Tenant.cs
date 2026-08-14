using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.Modules.Tenancy.Domain;

/// <summary>
/// Raiz do modulo: um estabelecimento assinante da plataforma. NAO implementa
/// ITenantOwned — Tenant E o tenant, nao pertence a um (nao tem RLS por
/// TenantId; e protegido por autorizacao de aplicacao, nao por linha).
/// </summary>
public sealed class Tenant : AggregateRoot<TenantId>, IAuditable, ISoftDeletable
{
    public string Name { get; private set; } = string.Empty;

    public Slug Slug { get; private set; } = null!;

    /// <summary>Dirige o vocabulario da UI (ver <see cref="BusinessTypeCatalog"/>) e o catalogo de servicos sugerido.</summary>
    public BusinessType BusinessType { get; private set; }

    /// <summary>Fuso horario IANA (ex.: America/Sao_Paulo) — toda conversao de data/hora do tenant parte daqui.</summary>
    public string TimeZoneId { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    /// <summary>
    /// Cor de marca em #RRGGBB, pareada com texto branco em toda a UI. Null usa
    /// a cor padrao da plataforma. Contraste AA e verificado no momento de
    /// salvar (<see cref="UpdateBranding"/>) — nunca depois.
    /// </summary>
    public string? PrimaryColorHex { get; private set; }

    /// <summary>Caminho publico do arquivo salvo por IFileStorage. Null usa um placeholder generico na UI.</summary>
    public string? LogoUrl { get; private set; }

    /// <summary>Imagem de capa da pagina publica (hero). Null usa um gradiente com a cor de marca.</summary>
    public string? BannerUrl { get; private set; }

    public string? Description { get; private set; }

    public PhoneNumber? Phone { get; private set; }

    /// <summary>Numero separado do Phone: nem todo estabelecimento atende WhatsApp no mesmo numero da recepcao.</summary>
    public PhoneNumber? WhatsApp { get; private set; }

    /// <summary>E-mail de contato do negocio — distinto do e-mail de login do dono (Identity.User).</summary>
    public Email? Email { get; private set; }

    public string? Address { get; private set; }

    public string? InstagramUrl { get; private set; }

    public string? FacebookUrl { get; private set; }

    /// <summary>
    /// Cor de apoio da pagina publica (badges, acentos). Mesma regra de
    /// contraste AA do PrimaryColorHex, pareada com texto branco.
    /// </summary>
    public string? SecondaryColorHex { get; private set; }

    public PublicPageFont Font { get; private set; } = PublicPageFont.Default;

    public PublicPageButtonStyle ButtonStyle { get; private set; } = PublicPageButtonStyle.Rounded;

    /// <summary>
    /// Visibilidade de cada secao da pagina publica. Default true em todas —
    /// a pagina ja funciona sem o dono configurar nada (a secao so aparece de
    /// fato quando ha dado para mostrar, ver Fase 2).
    /// </summary>
    public bool ShowAboutSection { get; private set; } = true;

    public bool ShowServicesSection { get; private set; } = true;

    public bool ShowTeamSection { get; private set; } = true;

    public bool ShowHoursSection { get; private set; } = true;

    public bool ShowContactSection { get; private set; } = true;

    private readonly List<BusinessHoursEntry> _businessHours = [];

    public IReadOnlyCollection<BusinessHoursEntry> BusinessHours => _businessHours;

    /// <summary>Minutos de intervalo exigidos apos cada agendamento antes do proximo poder comecar (mesmo recurso). 0 = sem intervalo.</summary>
    public int AppointmentBufferMinutes { get; private set; }

    private readonly List<ClosedDate> _closedDates = [];

    /// <summary>Datas especificas em que o estabelecimento nao atende (feriados, eventos) — ver <see cref="ClosedDate"/>.</summary>
    public IReadOnlyCollection<ClosedDate> ClosedDates => _closedDates;

    /// <summary>
    /// Integracao real com a WhatsApp Cloud API (Fase 6) — desligada por padrao,
    /// nenhuma mensagem sai sem o dono conectar o proprio numero. Distinto de
    /// <see cref="WhatsApp"/> (numero de contato exibido na pagina publica):
    /// isto e credencial de API pra ENVIAR mensagem automatica.
    /// </summary>
    public bool WhatsAppIntegrationEnabled { get; private set; }

    /// <summary>Identificador do numero na Cloud API (nao e o numero em si).</summary>
    public string? WhatsAppPhoneNumberId { get; private set; }

    /// <summary>Token de acesso da Cloud API — criptografado em coluna (ver TenancyDbContext), nunca logado nem devolvido em texto puro pela API.</summary>
    public string? WhatsAppAccessToken { get; private set; }

    /// <summary>Template customizado; null usa o texto padrao (ver WhatsAppMessageDefaults no modulo Scheduling).</summary>
    public string? WhatsAppScheduledTemplate { get; private set; }

    public string? WhatsAppReminderTemplate { get; private set; }

    public string? WhatsAppCancelledTemplate { get; private set; }

    public string? WhatsAppRescheduledTemplate { get; private set; }

    public string? WhatsAppConfirmedTemplate { get; private set; }

    public string? WhatsAppCompletedTemplate { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    // Exigido pelo EF Core para materializar a entidade vinda do banco.
    private Tenant()
    {
    }

    private Tenant(TenantId id, string name, Slug slug, BusinessType businessType, string timeZoneId) : base(id)
    {
        Name = name;
        Slug = slug;
        BusinessType = businessType;
        TimeZoneId = timeZoneId;
        IsActive = true;
    }

    public static Result<Tenant> Create(string? name, string? slugValue, BusinessType businessType, string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Tenant>(Error.Validation("Tenant.NameEmpty", "O nome do estabelecimento nao pode ser vazio."));
        }

        var slugResult = Slug.Create(slugValue);
        if (slugResult.IsFailure)
        {
            return Result.Failure<Tenant>(slugResult.Error);
        }

        if (!Enum.IsDefined(businessType))
        {
            return Result.Failure<Tenant>(Error.Validation("Tenant.InvalidBusinessType", "O segmento informado e invalido."));
        }

        if (string.IsNullOrWhiteSpace(timeZoneId) || !IsValidTimeZone(timeZoneId))
        {
            return Result.Failure<Tenant>(Error.Validation("Tenant.InvalidTimeZone", "O fuso horario informado e invalido."));
        }

        var tenant = new Tenant(TenantId.New(), name.Trim(), slugResult.Value, businessType, timeZoneId);
        tenant.Raise(new TenantCreatedDomainEvent(tenant.Id, tenant.Name, tenant.Slug.Value));

        return Result.Success(tenant);
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    /// <summary>Foreground fixo: todo componente da UI pareia --primary com texto branco (ver app/globals.css).</summary>
    private const string BrandForegroundHex = "#FFFFFF";

    public Result UpdateBranding(string? primaryColorHex)
    {
        if (!ContrastValidator.IsValidHexColor(primaryColorHex))
        {
            return Result.Failure(Error.Validation("Tenant.InvalidColorFormat", "Informe uma cor no formato #RRGGBB."));
        }

        if (!ContrastValidator.MeetsAaContrast(BrandForegroundHex, primaryColorHex!))
        {
            return Result.Failure(Error.Validation(
                "Tenant.InsufficientContrast",
                "Essa cor nao tem contraste suficiente com o texto branco (minimo AA: 4.5:1). Escolha um tom mais escuro."));
        }

        PrimaryColorHex = primaryColorHex;
        return Result.Success();
    }

    public Result SetLogo(string logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
        {
            return Result.Failure(Error.Validation("Tenant.InvalidLogoUrl", "URL do logo invalida."));
        }

        LogoUrl = logoUrl;
        return Result.Success();
    }

    public Result SetBanner(string bannerUrl)
    {
        if (string.IsNullOrWhiteSpace(bannerUrl))
        {
            return Result.Failure(Error.Validation("Tenant.InvalidBannerUrl", "URL do banner invalida."));
        }

        BannerUrl = bannerUrl;
        return Result.Success();
    }

    public Result UpdateProfile(
        string? description, string? phone, string? whatsApp, string? email, string? address, string? instagramUrl, string? facebookUrl)
    {
        var phoneResult = ParseOptionalPhone(phone);
        if (phoneResult.IsFailure)
        {
            return Result.Failure(phoneResult.Error);
        }

        var whatsAppResult = ParseOptionalPhone(whatsApp);
        if (whatsAppResult.IsFailure)
        {
            return Result.Failure(whatsAppResult.Error);
        }

        var emailResult = ParseOptionalEmail(email);
        if (emailResult.IsFailure)
        {
            return Result.Failure(emailResult.Error);
        }

        if (!IsValidOptionalUrl(instagramUrl))
        {
            return Result.Failure(Error.Validation("Tenant.InvalidInstagramUrl", "O link do Instagram precisa ser uma URL valida (http/https)."));
        }

        if (!IsValidOptionalUrl(facebookUrl))
        {
            return Result.Failure(Error.Validation("Tenant.InvalidFacebookUrl", "O link do Facebook precisa ser uma URL valida (http/https)."));
        }

        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Phone = phoneResult.Value;
        WhatsApp = whatsAppResult.Value;
        Email = emailResult.Value;
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        InstagramUrl = string.IsNullOrWhiteSpace(instagramUrl) ? null : instagramUrl.Trim();
        FacebookUrl = string.IsNullOrWhiteSpace(facebookUrl) ? null : facebookUrl.Trim();

        return Result.Success();
    }

    public Result UpdatePageCustomization(
        string? secondaryColorHex,
        PublicPageFont font,
        PublicPageButtonStyle buttonStyle,
        bool showAboutSection,
        bool showServicesSection,
        bool showTeamSection,
        bool showHoursSection,
        bool showContactSection)
    {
        if (secondaryColorHex is not null)
        {
            if (!ContrastValidator.IsValidHexColor(secondaryColorHex))
            {
                return Result.Failure(Error.Validation("Tenant.InvalidColorFormat", "Informe uma cor no formato #RRGGBB."));
            }

            if (!ContrastValidator.MeetsAaContrast(BrandForegroundHex, secondaryColorHex))
            {
                return Result.Failure(Error.Validation(
                    "Tenant.InsufficientContrast",
                    "Essa cor nao tem contraste suficiente com o texto branco (minimo AA: 4.5:1). Escolha um tom mais escuro."));
            }
        }

        if (!Enum.IsDefined(font))
        {
            return Result.Failure(Error.Validation("Tenant.InvalidFont", "A fonte informada e invalida."));
        }

        if (!Enum.IsDefined(buttonStyle))
        {
            return Result.Failure(Error.Validation("Tenant.InvalidButtonStyle", "O estilo de botao informado e invalido."));
        }

        SecondaryColorHex = secondaryColorHex;
        Font = font;
        ButtonStyle = buttonStyle;
        ShowAboutSection = showAboutSection;
        ShowServicesSection = showServicesSection;
        ShowTeamSection = showTeamSection;
        ShowHoursSection = showHoursSection;
        ShowContactSection = showContactSection;

        return Result.Success();
    }

    /// <summary>Substitui a semana inteira — o dono redefine tudo de uma vez, nao adiciona incrementalmente (mesmo padrao de Resource.SetWorkingHours).</summary>
    public Result SetBusinessHours(IReadOnlyList<(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime)> entries)
    {
        var parsedEntries = new List<BusinessHoursEntry>(entries.Count);

        foreach (var entry in entries)
        {
            var entryResult = BusinessHoursEntry.Create(entry.DayOfWeek, entry.StartTime, entry.EndTime);
            if (entryResult.IsFailure)
            {
                return Result.Failure(entryResult.Error);
            }

            parsedEntries.Add(entryResult.Value);
        }

        _businessHours.Clear();
        _businessHours.AddRange(parsedEntries);

        return Result.Success();
    }

    private const int MaxAppointmentBufferMinutes = 240;

    /// <summary>Substitui a lista inteira de datas fechadas de uma vez (mesmo padrao de SetBusinessHours).</summary>
    public Result UpdateSchedulingSettings(IReadOnlyList<(DateOnly Date, string? Reason)> closedDates, int appointmentBufferMinutes)
    {
        if (appointmentBufferMinutes < 0 || appointmentBufferMinutes > MaxAppointmentBufferMinutes)
        {
            return Result.Failure(Error.Validation(
                "Tenant.InvalidAppointmentBuffer", $"O intervalo entre agendamentos precisa estar entre 0 e {MaxAppointmentBufferMinutes} minutos."));
        }

        _closedDates.Clear();
        _closedDates.AddRange(closedDates.Select(d => ClosedDate.Create(d.Date, d.Reason)));
        AppointmentBufferMinutes = appointmentBufferMinutes;

        return Result.Success();
    }

    /// <summary>
    /// Substitui a configuracao inteira de uma vez (mesmo padrao de
    /// SetBusinessHours/UpdateSchedulingSettings). AccessToken chega ja
    /// resolvido pelo handler (mantem o token atual quando o dono nao digita
    /// um novo — ver UpdateTenantWhatsAppSettingsCommandHandler); aqui so
    /// valida a invariante "ligado precisa de credencial".
    /// </summary>
    public Result UpdateWhatsAppSettings(
        bool enabled,
        string? phoneNumberId,
        string? accessToken,
        string? scheduledTemplate,
        string? reminderTemplate,
        string? cancelledTemplate,
        string? rescheduledTemplate,
        string? confirmedTemplate,
        string? completedTemplate)
    {
        if (enabled && (string.IsNullOrWhiteSpace(phoneNumberId) || string.IsNullOrWhiteSpace(accessToken)))
        {
            return Result.Failure(Error.Validation(
                "Tenant.WhatsAppCredentialsRequired", "Para ativar o WhatsApp, informe o numero (Phone Number ID) e o token de acesso."));
        }

        WhatsAppIntegrationEnabled = enabled;
        WhatsAppPhoneNumberId = string.IsNullOrWhiteSpace(phoneNumberId) ? null : phoneNumberId.Trim();
        WhatsAppAccessToken = string.IsNullOrWhiteSpace(accessToken) ? null : accessToken.Trim();
        WhatsAppScheduledTemplate = string.IsNullOrWhiteSpace(scheduledTemplate) ? null : scheduledTemplate.Trim();
        WhatsAppReminderTemplate = string.IsNullOrWhiteSpace(reminderTemplate) ? null : reminderTemplate.Trim();
        WhatsAppCancelledTemplate = string.IsNullOrWhiteSpace(cancelledTemplate) ? null : cancelledTemplate.Trim();
        WhatsAppRescheduledTemplate = string.IsNullOrWhiteSpace(rescheduledTemplate) ? null : rescheduledTemplate.Trim();
        WhatsAppConfirmedTemplate = string.IsNullOrWhiteSpace(confirmedTemplate) ? null : confirmedTemplate.Trim();
        WhatsAppCompletedTemplate = string.IsNullOrWhiteSpace(completedTemplate) ? null : completedTemplate.Trim();

        return Result.Success();
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

    private static Result<Email?> ParseOptionalEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Success<Email?>(null);
        }

        var result = Email.Create(email);
        return result.IsSuccess ? Result.Success<Email?>(result.Value) : Result.Failure<Email?>(result.Error);
    }

    private static bool IsValidOptionalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var parsed) && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);
    }

    private static bool IsValidTimeZone(string timeZoneId)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
