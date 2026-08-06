using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.SharedKernel.Messaging;

public static class DispatcherServiceCollectionExtensions
{
    public static IServiceCollection AddDispatcher(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        return services;
    }

    /// <summary>
    /// Registra todo ICommandHandler/IQueryHandler/IPipelineBehavior encontrado no
    /// assembly informado. Cada modulo chama isso apenas para o PROPRIO assembly —
    /// a varredura nunca cruza a fronteira de modulo, entao um handler do modulo A
    /// jamais e resolvido para um request do modulo B.
    /// </summary>
    public static IServiceCollection AddHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        RegisterOpenGenericImplementations(services, assembly, typeof(IRequestHandler<,>));
        RegisterOpenGenericImplementations(services, assembly, typeof(IPipelineBehavior<,>));
        return services;
    }

    private static void RegisterOpenGenericImplementations(IServiceCollection services, Assembly assembly, Type openGenericInterface)
    {
        var candidateTypes = assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false });

        foreach (var type in candidateTypes)
        {
            var matchingInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericInterface);

            foreach (var matchingInterface in matchingInterfaces)
            {
                services.AddTransient(matchingInterface, type);
            }
        }
    }
}
