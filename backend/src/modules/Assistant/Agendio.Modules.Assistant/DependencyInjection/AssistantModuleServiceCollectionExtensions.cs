using System.Reflection;
using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Assistant.Endpoints;
using Agendio.SharedKernel.Messaging;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.Modules.Assistant.DependencyInjection;

// Sem DbContext: o modulo Assistant nao tem nenhuma entidade persistida (Fase
// 22 — sem historico de conversa no servidor, ver escopo no handler). So
// orquestra leituras de outros modulos (.Contracts) e a chamada de IA.
public static class AssistantModuleServiceCollectionExtensions
{
    public static IServiceCollection AddAssistantModule(this IServiceCollection services)
    {
        var moduleAssembly = Assembly.GetExecutingAssembly();

        services.AddValidatorsFromAssembly(moduleAssembly);
        services.AddHandlersFromAssembly(moduleAssembly);

        services.AddSingleton<IEndpointModule, AssistantEndpoints>();

        return services;
    }
}
