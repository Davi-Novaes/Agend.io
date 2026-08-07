using System.Reflection;
using Agendio.Infrastructure.DependencyInjection;
using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Billing.Contracts;
using Agendio.Modules.Billing.Endpoints;
using Agendio.Modules.Billing.Infrastructure.Asaas;
using Agendio.Modules.Billing.Infrastructure.Jobs;
using Agendio.Modules.Billing.Infrastructure.Messaging;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Agendio.Modules.Billing.DependencyInjection;

public static class BillingModuleServiceCollectionExtensions
{
    public static IServiceCollection AddBillingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var moduleAssembly = Assembly.GetExecutingAssembly();

        services.AddModuleDbContext<BillingDbContext>(configuration);
        services.AddOutboxProcessing<BillingDbContext>();

        services.AddValidatorsFromAssembly(moduleAssembly);
        services.AddHandlersFromAssembly(moduleAssembly);

        services.AddSingleton<IEndpointModule, BillingEndpoints>();
        services.AddScoped<IBillingAdministrationService, Infrastructure.BillingAdministrationService>();

        services.Configure<AsaasOptions>(configuration.GetSection(AsaasOptions.SectionName));
        services.AddHttpClient<IAsaasClient, AsaasClient>((serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<AsaasOptions>>().Value;
            httpClient.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            httpClient.DefaultRequestHeaders.Add("access_token", options.ApiKey);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Agendio");
        });

        services.AddHostedService<BillingIntegrationEventConsumer>();
        services.AddScoped<BillingReconciliationJob>();

        return services;
    }
}
