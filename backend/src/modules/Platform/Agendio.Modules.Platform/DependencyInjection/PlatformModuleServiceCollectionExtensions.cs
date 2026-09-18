using System.Reflection;
using Agendio.Infrastructure.DependencyInjection;
using Agendio.Infrastructure.Endpoints;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Platform.Endpoints;
using Agendio.Modules.Platform.Infrastructure.Jobs;
using Agendio.Modules.Platform.Infrastructure.Mfa;
using Agendio.Modules.Platform.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.Modules.Platform.DependencyInjection;

public static class PlatformModuleServiceCollectionExtensions
{
    public static IServiceCollection AddPlatformModule(this IServiceCollection services, IConfiguration configuration)
    {
        var moduleAssembly = Assembly.GetExecutingAssembly();

        services.AddModuleDbContext<PlatformDbContext>(configuration);
        services.AddOutboxProcessing<PlatformDbContext>();

        services.AddValidatorsFromAssembly(moduleAssembly);
        services.AddHandlersFromAssembly(moduleAssembly);

        services.AddSingleton<IEndpointModule, PlatformEndpoints>();

        // Mesmo raciocinio de IdentityModuleServiceCollectionExtensions: IPlatformMfaChallengeStore/
        // IPlatformMfaCodeVerifier sao compartilhados entre handlers (login sem MFA, verify,
        // disable) — nao sao ICommandHandler/IQueryHandler, AddHandlersFromAssembly nao os enxerga.
        services.AddSingleton<IPlatformMfaChallengeStore, RedisPlatformMfaChallengeStore>();
        services.AddScoped<IPlatformMfaCodeVerifier, PlatformMfaCodeVerifier>();

        // Tipo fechado direto — ver comentario equivalente em
        // IdentityModuleServiceCollectionExtensions sobre por que uma
        // interface ISecurityAuditLogger compartilhada entre modulos e um bug.
        services.AddScoped<SecurityAuditLogger<PlatformDbContext>>();

        services.AddScoped<SecurityAlertJob>();

        return services;
    }
}
