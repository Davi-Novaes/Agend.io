using System.Reflection;
using Agendio.Infrastructure.DependencyInjection;
using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Catalog.Contracts;
using Agendio.Modules.Catalog.Endpoints;
using Agendio.Modules.Catalog.Infrastructure;
using Agendio.Modules.Catalog.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.Modules.Catalog.DependencyInjection;

public static class CatalogModuleServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        var moduleAssembly = Assembly.GetExecutingAssembly();

        services.AddModuleDbContext<CatalogDbContext>(configuration);
        services.AddOutboxProcessing<CatalogDbContext>();

        services.AddValidatorsFromAssembly(moduleAssembly);
        services.AddHandlersFromAssembly(moduleAssembly);

        services.AddSingleton<IEndpointModule, ServiceEndpoints>();
        services.AddSingleton<IEndpointModule, PublicCatalogEndpoints>();
        services.AddScoped<IServiceLookupService, ServiceLookupService>();

        return services;
    }
}
