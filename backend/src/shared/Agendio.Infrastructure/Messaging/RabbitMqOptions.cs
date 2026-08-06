namespace Agendio.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public required string HostName { get; init; }

    public int Port { get; init; } = 5672;

    public required string UserName { get; init; }

    public required string Password { get; init; }

    /// <summary>Exchange topico unico para todos os integration events — routing key = nome do evento.</summary>
    public string ExchangeName { get; init; } = "agendio.events";
}
