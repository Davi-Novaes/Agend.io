using Agendio.Infrastructure.Messaging;
using Agendio.Infrastructure.Persistence;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.Infrastructure.DependencyInjection;

public static class HangfireServiceCollectionExtensions
{
    /// <summary>
    /// Ativa o job recorrente que drena a tabela outbox do modulo para o
    /// RabbitMQ (ver OutboxProcessor e o comentario em OutboxMessage). A
    /// tabela/o interceptor que grava nela ja existiam desde o Sprint 0 — so
    /// faltava alguem de fato ler e publicar, o que so passou a importar com
    /// o primeiro consumidor real (notificacoes, Sprint 5).
    ///
    /// Usa IRecurringJobManager (resolvido do DI), nao a fachada estatica
    /// RecurringJob — esta ultima depende de JobStorage.Current, que so fica
    /// disponivel depois que o pipeline do Hangfire termina de subir, e falha
    /// logo apos app.Build() com "JobStorage instance has not been initialized".
    /// </summary>
    public static void ScheduleOutboxProcessing<TDbContext>(this IServiceProvider services, string moduleName)
        where TDbContext : AgendioDbContextBase
    {
        var recurringJobManager = services.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate<OutboxProcessor<TDbContext>>(
            $"outbox-{moduleName}",
            processor => processor.ProcessPendingMessagesAsync(CancellationToken.None),
            Cron.Minutely());
    }

    /// <summary>
    /// Storage no mesmo PostgreSQL da aplicacao — sem infra extra so para jobs.
    /// O dashboard (habilitado no Agendio.Api, restrito a Super Admin) da
    /// visibilidade operacional sobre falha/retry de job, o que pesou a favor de
    /// Hangfire em vez de Quartz.NET (ver docs/adr).
    ///
    /// Usa "PostgresAdmin" (role dona do banco), NAO "Postgres" (role da
    /// aplicacao): o Hangfire precisa de DDL para criar/manter o proprio schema
    /// na primeira execucao (PrepareSchemaIfNecessary). Isso e uma excecao
    /// deliberada e segura — as tabelas do Hangfire nao guardam dado de tenant,
    /// entao nao fazem parte do perimetro de Row Level Security.
    /// </summary>
    public static IServiceCollection AddAgendioHangfire(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PostgresAdmin")
            ?? throw new InvalidOperationException("Connection string 'PostgresAdmin' nao configurada.");

        services.AddHangfire(hangfireConfig => hangfireConfig
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer();

        return services;
    }
}
