using System.Reflection;
using Agendio.Infrastructure.DependencyInjection;
using Agendio.Infrastructure.Endpoints;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Application;
using Agendio.Modules.Identity.Contracts;
using Agendio.Modules.Identity.Endpoints;
using Agendio.Modules.Identity.Infrastructure;
using Agendio.Modules.Identity.Infrastructure.Mfa;
using Agendio.Modules.Identity.Infrastructure.Notifications;
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
        services.AddScoped<EmailConfirmationJobs>();
        services.AddScoped<PasswordResetJobs>();

        // Tipo fechado direto (nao uma interface ISecurityAuditLogger
        // compartilhada) — ver comentario de SecurityAuditLogger<TContext>
        // sobre por que isso importa: duas modulos registrando a mesma
        // interface fariam o ultimo "vencer" para qualquer injecao no processo.
        services.AddScoped<SecurityAuditLogger<IdentityDbContext>>();

        // Unico ponto de leitura sincrona que o Platform (painel do Super
        // Admin) tem sobre a trilha de seguranca de Identity — ver
        // ISecurityAuditReader.
        services.AddScoped<ISecurityAuditReader, SecurityAuditReader>();

        return services;
    }
}
