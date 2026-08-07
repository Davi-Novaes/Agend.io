using System.Reflection;
using Agendio.Infrastructure.DependencyInjection;
using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Customers.Endpoints;
using Agendio.SharedKernel.Messaging;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.Modules.Customers.DependencyInjection;

public static class CustomersModuleServiceCollectionExtensions
{
    public static IServiceCollection AddCustomersModule(this IServiceCollection services, IConfiguration configuration)
    {
        var moduleAssembly = Assembly.GetExecutingAssembly();

        services.AddModuleDbContext<Infrastructure.Persistence.CustomersDbContext>(configuration);
        services.AddOutboxProcessing<Infrastructure.Persistence.CustomersDbContext>();

        services.AddValidatorsFromAssembly(moduleAssembly);
        services.AddHandlersFromAssembly(moduleAssembly);

        services.AddSingleton<IEndpointModule, CustomerEndpoints>();
        services.AddScoped<ICustomerLookupService, Infrastructure.CustomerLookupService>();
        services.AddScoped<ICustomerRegistrationService, Infrastructure.CustomerRegistrationService>();

        return services;
    }
}
