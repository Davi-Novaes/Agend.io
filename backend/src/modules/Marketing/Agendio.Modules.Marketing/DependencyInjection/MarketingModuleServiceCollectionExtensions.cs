using System.Reflection;
using Agendio.Infrastructure.DependencyInjection;
using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Marketing.Endpoints;
using Agendio.Modules.Marketing.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.Modules.Marketing.DependencyInjection;

public static class MarketingModuleServiceCollectionExtensions
{
    public static IServiceCollection AddMarketingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var moduleAssembly = Assembly.GetExecutingAssembly();

        services.AddModuleDbContext<MarketingDbContext>(configuration);
        services.AddOutboxProcessing<MarketingDbContext>();

        services.AddValidatorsFromAssembly(moduleAssembly);
        services.AddHandlersFromAssembly(moduleAssembly);

        services.AddSingleton<IEndpointModule, MarketingEndpoints>();

        return services;
    }
}
