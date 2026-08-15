using System.Reflection;
using Agendio.Infrastructure.DependencyInjection;
using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Estoque.Contracts;
using Agendio.Modules.Estoque.Endpoints;
using Agendio.Modules.Estoque.Infrastructure;
using Agendio.Modules.Estoque.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.Modules.Estoque.DependencyInjection;

public static class EstoqueModuleServiceCollectionExtensions
{
    public static IServiceCollection AddEstoqueModule(this IServiceCollection services, IConfiguration configuration)
    {
        var moduleAssembly = Assembly.GetExecutingAssembly();

        services.AddModuleDbContext<EstoqueDbContext>(configuration);
        services.AddOutboxProcessing<EstoqueDbContext>();

        services.AddValidatorsFromAssembly(moduleAssembly);
        services.AddHandlersFromAssembly(moduleAssembly);

        services.AddSingleton<IEndpointModule, EstoqueEndpoints>();

        services.AddScoped<IInventorySummaryLookupService, InventorySummaryLookupService>();

        return services;
    }
}
