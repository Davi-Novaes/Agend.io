namespace Agendio.Modules.Feedback;

/// <summary>Endereco que recebe um e-mail a cada feedback novo, alem de ficar no painel do Super Admin.</summary>
public sealed class FeedbackNotificationOptions
{
    public const string SectionName = "Feedback";

    public required string NotificationEmail { get; init; }
}
