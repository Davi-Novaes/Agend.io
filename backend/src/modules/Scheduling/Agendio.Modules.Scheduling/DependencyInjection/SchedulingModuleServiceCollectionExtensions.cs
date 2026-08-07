using System.Reflection;
using Agendio.Infrastructure.DependencyInjection;
using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Scheduling.Endpoints;
using Agendio.Modules.Scheduling.Infrastructure.Notifications;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.Modules.Scheduling.DependencyInjection;

public static class SchedulingModuleServiceCollectionExtensions
{
    public static IServiceCollection AddSchedulingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var moduleAssembly = Assembly.GetExecutingAssembly();

        services.AddModuleDbContext<SchedulingDbContext>(configuration);
        services.AddOutboxProcessing<SchedulingDbContext>();

        services.AddValidatorsFromAssembly(moduleAssembly);
        services.AddHandlersFromAssembly(moduleAssembly);

        services.AddSingleton<IEndpointModule, AppointmentEndpoints>();
        services.AddSingleton<IEndpointModule, PublicAppointmentEndpoints>();
        services.AddScoped<AppointmentNotificationJobs>();

        return services;
    }
}
