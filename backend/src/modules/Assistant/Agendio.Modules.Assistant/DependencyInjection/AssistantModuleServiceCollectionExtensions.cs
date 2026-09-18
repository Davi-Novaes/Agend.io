using System.Reflection;
using Agendio.Infrastructure.DependencyInjection;
using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Assistant.Application.AskAssistant.IntentMatching;
using Agendio.Modules.Assistant.Application.AskAssistant.IntentMatching.IntentRules;
using Agendio.Modules.Assistant.Endpoints;
using Agendio.Modules.Assistant.Infrastructure;
using Agendio.Modules.Assistant.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.Modules.Assistant.DependencyInjection;

public static class AssistantModuleServiceCollectionExtensions
{
    public static IServiceCollection AddAssistantModule(this IServiceCollection services, IConfiguration configuration)
    {
        var moduleAssembly = Assembly.GetExecutingAssembly();

        // Fase 3: primeiro DbContext do modulo, so pra log de auditoria (ver
        // AssistantQueryLogEntry) -- SEM AddOutboxProcessing de proposito, o
        // modulo nunca publica integration event nenhum (a tabela outbox fica
        // la, herdada de AgendioDbContextBase, mas sem nenhum processo lendo
        // ela, custo zero).
        services.AddModuleDbContext<AssistantDbContext>(configuration);

        services.AddValidatorsFromAssembly(moduleAssembly);
        services.AddHandlersFromAssembly(moduleAssembly);

        // Ordem de registro = ordem de tentativa do IntentMatcher (regras mais
        // especificas primeiro -- Navigation antes pra "como cadastro um
        // cliente" nao cair em CustomerCount por engano, ja que ambas mencionam
        // "cliente").
        services.AddSingleton<IIntentRule, NavigationIntentRule>();
        services.AddScoped<IIntentRule, CustomerCountIntentRule>();
        services.AddScoped<IIntentRule, InactiveCustomersIntentRule>();
        services.AddScoped<IIntentRule, LowStockListIntentRule>();
        services.AddScoped<IIntentRule, ProfessionalRankingIntentRule>();
        services.AddScoped<IIntentRule, TopServiceIntentRule>();
        services.AddScoped<IIntentRule, AppointmentCountIntentRule>();
        services.AddScoped<IIntentRule, RevenueIntentRule>();
        services.AddScoped<IIntentRule, ExpensesIntentRule>();
        services.AddScoped<IntentMatcher>();

        services.AddScoped<IAssistantQueryLogger, AssistantQueryLogger>();

        services.AddSingleton<IEndpointModule, AssistantEndpoints>();

        return services;
    }
}
