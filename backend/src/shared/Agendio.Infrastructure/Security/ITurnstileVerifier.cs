namespace Agendio.Infrastructure.Security;

public interface ITurnstileVerifier
{
    Task<bool> VerifyAsync(string? token, CancellationToken cancellationToken = default);
}
