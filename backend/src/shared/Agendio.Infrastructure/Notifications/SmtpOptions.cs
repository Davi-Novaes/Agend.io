namespace Agendio.Infrastructure.Notifications;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public required string Host { get; init; }

    public int Port { get; init; } = 1025;

    // MailHog (dev) nao exige autenticacao. Um provedor real de producao
    // preencheria estes dois via secret, nunca em appsettings versionado.
    public string? UserName { get; init; }

    public string? Password { get; init; }

    public required string FromAddress { get; init; }

    public string FromName { get; init; } = "Agendio";
}
