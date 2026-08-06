using System.Reflection;
using Agendio.Infrastructure.DependencyInjection;
using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.Modules.Tenancy.Endpoints;
using Agendio.Modules.Tenancy.Infrastructure;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.Modules.Tenancy.DependencyInjection;

public static class TenancyModuleServiceCollectionExtensions
{
    public static IServiceCollection AddTenancyModule(this IServiceCollection services, IConfiguration configuration)
    {
        var moduleAssembly = Assembly.GetExecutingAssembly();

        services.AddModuleDbContext<TenancyDbContext>(configuration);
        services.AddOutboxProcessing<TenancyDbContext>();

        services.AddScoped<ITenantLookupService, TenantLookupService>();

        services.AddValidatorsFromAssembly(moduleAssembly);
        services.AddHandlersFromAssembly(moduleAssembly);

        services.AddSingleton<IEndpointModule, TenancyEndpoints>();

        return services;
    }
}
