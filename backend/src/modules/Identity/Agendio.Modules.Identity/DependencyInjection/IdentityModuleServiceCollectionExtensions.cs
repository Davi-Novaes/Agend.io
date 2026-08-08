using System.Reflection;
using Agendio.Infrastructure.DependencyInjection;
using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Identity.Application;
using Agendio.Modules.Identity.Endpoints;
using Agendio.Modules.Identity.Infrastructure.Mfa;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.Modules.Identity.DependencyInjection;

public static class IdentityModuleServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        var moduleAssembly = Assembly.GetExecutingAssembly();

        services.AddModuleDbContext<IdentityDbContext>(configuration);
        services.AddOutboxProcessing<IdentityDbContext>();

        services.AddValidatorsFromAssembly(moduleAssembly);
        services.AddHandlersFromAssembly(moduleAssembly);

        services.AddSingleton<IEndpointModule, IdentityEndpoints>();

        // "Emitir tokens para um usuario ja autenticado" e IMfaCodeVerifier
        // (TOTP + codigo de recuperacao) sao compartilhados entre handlers (login
        // sem MFA, verify, disable) — nao sao ICommandHandler/IQueryHandler, entao
        // AddHandlersFromAssembly nao os enxerga; registro explicito.
        services.AddScoped<AuthTokenIssuer>();
        services.AddSingleton<IMfaChallengeStore, RedisMfaChallengeStore>();
        services.AddScoped<IMfaCodeVerifier, MfaCodeVerifier>();

        return services;
    }
}
