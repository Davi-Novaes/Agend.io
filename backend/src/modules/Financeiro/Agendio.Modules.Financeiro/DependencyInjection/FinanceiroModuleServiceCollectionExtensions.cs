using System.Reflection;
using Agendio.Infrastructure.DependencyInjection;
using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Financeiro.Contracts;
using Agendio.Modules.Financeiro.Endpoints;
using Agendio.Modules.Financeiro.Infrastructure;
using Agendio.Modules.Financeiro.Infrastructure.Messaging;
using Agendio.Modules.Financeiro.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.Modules.Financeiro.DependencyInjection;

public static class FinanceiroModuleServiceCollectionExtensions
{
    public static IServiceCollection AddFinanceiroModule(this IServiceCollection services, IConfiguration configuration)
    {
        var moduleAssembly = Assembly.GetExecutingAssembly();

        services.AddModuleDbContext<FinanceiroDbContext>(configuration);
        services.AddOutboxProcessing<FinanceiroDbContext>();

        services.AddValidatorsFromAssembly(moduleAssembly);
        services.AddHandlersFromAssembly(moduleAssembly);

        services.AddSingleton<IEndpointModule, FinanceiroEndpoints>();

        services.AddScoped<IFinanceSummaryLookupService, FinanceSummaryLookupService>();

        services.AddHostedService<FinancialIntegrationEventConsumer>();

        return services;
    }
}
