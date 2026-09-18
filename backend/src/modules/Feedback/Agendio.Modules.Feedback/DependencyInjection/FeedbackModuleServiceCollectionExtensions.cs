using System.Reflection;
using Agendio.Infrastructure.DependencyInjection;
using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Feedback.Contracts;
using Agendio.Modules.Feedback.Endpoints;
using Agendio.Modules.Feedback.Infrastructure;
using Agendio.Modules.Feedback.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.Modules.Feedback.DependencyInjection;

public static class FeedbackModuleServiceCollectionExtensions
{
    public static IServiceCollection AddFeedbackModule(this IServiceCollection services, IConfiguration configuration)
    {
        var moduleAssembly = Assembly.GetExecutingAssembly();

        services.AddModuleDbContext<FeedbackDbContext>(configuration);
        services.AddOutboxProcessing<FeedbackDbContext>();

        services.AddValidatorsFromAssembly(moduleAssembly);
        services.AddHandlersFromAssembly(moduleAssembly);

        services.Configure<FeedbackNotificationOptions>(configuration.GetSection(FeedbackNotificationOptions.SectionName));

        services.AddScoped<IFeedbackReader, FeedbackReader>();

        services.AddSingleton<IEndpointModule, FeedbackEndpoints>();

        return services;
    }
}
