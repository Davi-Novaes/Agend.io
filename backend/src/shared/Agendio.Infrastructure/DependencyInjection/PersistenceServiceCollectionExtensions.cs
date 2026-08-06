using Agendio.Infrastructure.Messaging;
using Agendio.Infrastructure.Persistence;
using Agendio.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.Infrastructure.DependencyInjection;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registra o DbContext de UM modulo com a stack padrao: Npgsql, convencao
    /// snake_case (idiomatica no Postgres e o que os scripts de RLS esperam) e os
    /// tres interceptors transversais. Cada modulo chama isto para o proprio
    /// DbContext — nunca existe um DbContext compartilhado entre modulos.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "Postgres")
        where TDbContext : DbContext
    {
        var connectionString = configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{connectionStringName}' nao configurada.");

        services.AddDbContext<TDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure());
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(
                serviceProvider.GetRequiredService<TenantConnectionInterceptor>(),
                serviceProvider.GetRequiredService<AuditingSaveChangesInterceptor>(),
                serviceProvider.GetRequiredService<DomainEventsToOutboxInterceptor>());
        });

        return services;
    }

    /// <summary>Habilita o processamento de outbox (leitura + publicacao no RabbitMQ) para o DbContext do modulo.</summary>
    public static IServiceCollection AddOutboxProcessing<TDbContext>(this IServiceCollection services)
        where TDbContext : AgendioDbContextBase
    {
        services.AddScoped<OutboxProcessor<TDbContext>>();
        return services;
    }
}
