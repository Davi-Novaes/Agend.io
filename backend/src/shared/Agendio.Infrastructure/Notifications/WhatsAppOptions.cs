namespace Agendio.Infrastructure.Notifications;

/// <summary>So a URL base da Cloud API — sem segredo aqui (credenciais sao por tenant, ver IWhatsAppSender). Default ja funciona sem nenhuma configuracao extra em appsettings.</summary>
public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    public string BaseUrl { get; init; } = "https://graph.facebook.com/v20.0/";
}
