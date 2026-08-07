using System.Reflection;
using Agendio.Infrastructure.DependencyInjection;
using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Resources.Contracts;
using Agendio.Modules.Resources.Endpoints;
using Agendio.Modules.Resources.Infrastructure;
using Agendio.Modules.Resources.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.Modules.Resources.DependencyInjection;

public static class ResourcesModuleServiceCollectionExtensions
{
    public static IServiceCollection AddResourcesModule(this IServiceCollection services, IConfiguration configuration)
    {
        var moduleAssembly = Assembly.GetExecutingAssembly();

        services.AddModuleDbContext<ResourcesDbContext>(configuration);
        services.AddOutboxProcessing<ResourcesDbContext>();

        services.AddValidatorsFromAssembly(moduleAssembly);
        services.AddHandlersFromAssembly(moduleAssembly);

        services.AddSingleton<IEndpointModule, ResourceEndpoints>();
        services.AddSingleton<IEndpointModule, PublicResourceEndpoints>();
        services.AddScoped<IResourceLookupService, ResourceLookupService>();

        return services;
    }
}
