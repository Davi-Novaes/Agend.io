using System.Net.Http.Headers;
using Agendio.Infrastructure.AiAssistant;
using Agendio.Infrastructure.AiAssistant.Providers;
using Agendio.Infrastructure.Behaviors;
using Agendio.Infrastructure.Messaging;
using Agendio.Infrastructure.Multitenancy;
using Agendio.Infrastructure.Notifications;
using Agendio.Infrastructure.Payments;
using Agendio.Infrastructure.Persistence.Interceptors;
using Agendio.Infrastructure.Security;
using Agendio.Infrastructure.Storage;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Agendio.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registra tudo que e transversal a todos os modulos: relogio, contexto de
    /// tenant, seguranca (hash de senha, JWT), mensageria e os interceptors de
    /// EF Core. Cada modulo, alem disso, registra o PROPRIO DbContext e os
    /// PROPRIOS handlers via AddHandlersFromAssembly.
    /// </summary>
    public static IServiceCollection AddAgendioInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ITenantContext, HttpTenantContext>();

        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        services.Configure<PlatformJwtOptions>(configuration.GetSection(PlatformJwtOptions.SectionName));
        services.AddSingleton<IPlatformJwtTokenService, PlatformJwtTokenService>();

        services.Configure<OnboardingJwtOptions>(configuration.GetSection(OnboardingJwtOptions.SectionName));
        services.AddSingleton<IOnboardingJwtTokenService, OnboardingJwtTokenService>();

        services.Configure<ColumnEncryptionOptions>(configuration.GetSection(ColumnEncryptionOptions.SectionName));
        services.AddSingleton<IEncryptionService, AesGcmEncryptionService>();

        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton<IIntegrationEventPublisher, RabbitMqIntegrationEventPublisher>();

        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.AddTransient<IEmailSender, SmtpEmailSender>();
        services.Configure<FrontendOptions>(configuration.GetSection(FrontendOptions.SectionName));

        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));
        services.AddHttpClient<IWhatsAppSender, WhatsAppCloudApiSender>((serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<WhatsAppOptions>>().Value;
            httpClient.BaseAddress = new Uri(options.BaseUrl);
        });

        services.Configure<PaymentGatewayOptions>(configuration.GetSection(PaymentGatewayOptions.SectionName));
        services.AddHttpClient<IPaymentChargeClient, AsaasPaymentChargeClient>((serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<PaymentGatewayOptions>>().Value;
            httpClient.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            httpClient.DefaultRequestHeaders.Add("access_token", options.ApiKey);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Agendio");
        });

        services.Configure<AiAssistantOptions>(configuration.GetSection(AiAssistantOptions.SectionName));
        AddAiChatClients(services);

        services.AddSingleton<IFileStorage, LocalFileStorage>();

        AddRedis(services, configuration);

        // Interceptors do EF Core: escopados porque dependem de ITenantContext
        // (escopado por requisicao). Cada DbContext de modulo os injeta via
        // options.AddInterceptors(...) na propria configuracao (AddDbContext).
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddScoped<AuditingSaveChangesInterceptor>();
        services.AddScoped<AuditLogInterceptor>();
        services.AddScoped<DomainEventsToOutboxInterceptor>();

        services.AddDispatcher();

        // Pipeline behaviors GLOBAIS: registro como generico aberto aplica a
        // TODO comando/consulta de TODO modulo, sem cada modulo precisar repetir.
        // A ORDEM de registro e a ordem de execucao (o primeiro registrado e o
        // mais externo): Logging precisa envolver tudo (inclusive falha de
        // validacao) para registrar o desfecho; ExplicitTenant roda por ultimo,
        // logo antes do handler, para o tenant estar ancorado quando o handler
        // tocar o banco.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ExplicitTenantBehavior<,>));

        return services;
    }

    // Os 3 clientes concretos ficam sempre registrados (Fase 22: usuario pediu
    // suporte simultaneo a Anthropic/OpenAI/DeepSeek); IAiChatClient resolve pro
    // que estiver ativo em AiAssistantOptions.Provider, sem o modulo Assistant
    // precisar saber qual e.
    private static void AddAiChatClients(IServiceCollection services)
    {
        services.AddHttpClient<AnthropicChatClient>((serviceProvider, httpClient) =>
        {
            var aiOptions = serviceProvider.GetRequiredService<IOptions<AiAssistantOptions>>().Value;
            httpClient.BaseAddress = new Uri("https://api.anthropic.com/");
            httpClient.DefaultRequestHeaders.Add("x-api-key", aiOptions.ApiKey);
            httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        });

        services.AddHttpClient<OpenAiChatClient>((serviceProvider, httpClient) =>
        {
            var aiOptions = serviceProvider.GetRequiredService<IOptions<AiAssistantOptions>>().Value;
            httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aiOptions.ApiKey);
        });

        services.AddHttpClient<DeepSeekChatClient>((serviceProvider, httpClient) =>
        {
            var aiOptions = serviceProvider.GetRequiredService<IOptions<AiAssistantOptions>>().Value;
            httpClient.BaseAddress = new Uri("https://api.deepseek.com/");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aiOptions.ApiKey);
        });

        services.AddScoped<IAiChatClient>(serviceProvider =>
        {
            var provider = serviceProvider.GetRequiredService<IOptions<AiAssistantOptions>>().Value.Provider;
            return provider switch
            {
                AiAssistantProvider.OpenAi => serviceProvider.GetRequiredService<OpenAiChatClient>(),
                AiAssistantProvider.DeepSeek => serviceProvider.GetRequiredService<DeepSeekChatClient>(),
                _ => serviceProvider.GetRequiredService<AnthropicChatClient>(),
            };
        });
    }

    private static void AddRedis(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Connection string 'Redis' nao configurada.");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(connectionString));

        services.AddStackExchangeRedisCache(redisOptions =>
        {
            redisOptions.Configuration = connectionString;
            redisOptions.InstanceName = "agendio:";
        });
    }
}
