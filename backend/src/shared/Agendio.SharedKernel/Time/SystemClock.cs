namespace Agendio.SharedKernel.Time;

/// <summary>
/// Implementacao real, registrada em producao. Testes usam um FakeClock proprio
/// (em Agendio.UnitTests) para controlar o tempo de forma deterministica.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
