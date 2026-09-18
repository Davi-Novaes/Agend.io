using Agendio.Modules.Platform.Domain;

namespace Agendio.Modules.Platform.Infrastructure.Mfa;

public interface IPlatformMfaCodeVerifier
{
    bool Verify(PlatformAdmin admin, string code);
}
