using System.Globalization;
using Agendio.Infrastructure.Multitenancy;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Billing.Infrastructure.Asaas;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.Modules.Catalog.Infrastructure.Persistence;
using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.Modules.Platform.Infrastructure.Persistence;
using Agendio.Modules.Resources.Infrastructure.Persistence;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace Agendio.IntegrationTests;

/// <summary>
/// Sobe Postgres + Redis + RabbitMQ reais via Testcontainers e o host inteiro da
/// Agendio.Api em cima deles — sem isso nao da pra testar RLS de verdade (um
/// Postgres in-memory/fake nao aplica politica nenhuma).
///
/// Postgres/Redis/RabbitMq sao necessarios TODOS OS TRES so para o host subir:
/// AddAgendioInfrastructure conecta ao Redis de forma sincrona/eager no Build(),
/// entao nao da pra "pular" nenhuma dessas dependencias nos testes de API.
/// </summary>
public sealed class IntegrationTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string OwnerUsername = "agendio_owner";
    private const string OwnerPassword = "agendio_owner_dev_only_test";
    private const string AppUsername = "agendio_app";
    private const string AppPassword = "agendio_dev_only_test";

    // Mantido em sincronia manual com infra/postgres/init/01-roles-and-database.sql.
    // Duplicado aqui (em vez de ler o arquivo do disco) para o teste nao depender
    // de qual diretorio o test runner considera "atual".
    private static readonly string BootstrapSql = $"""
        CREATE ROLE {OwnerUsername} WITH LOGIN PASSWORD '{OwnerPassword}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
        CREATE ROLE {AppUsername}   WITH LOGIN PASSWORD '{AppPassword}'   NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;

        ALTER DATABASE agendio OWNER TO {OwnerUsername};
        GRANT ALL ON SCHEMA public TO {OwnerUsername};

        CREATE EXTENSION IF NOT EXISTS btree_gist;

        GRANT CONNECT ON DATABASE agendio TO {AppUsername};
        GRANT USAGE ON SCHEMA public TO {AppUsername};

        ALTER DEFAULT PRIVILEGES FOR ROLE {OwnerUsername} GRANT USAGE ON SCHEMAS TO {AppUsername};
        ALTER DEFAULT PRIVILEGES FOR ROLE {OwnerUsername} GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {AppUsername};
        ALTER DEFAULT PRIVILEGES FOR ROLE {OwnerUsername} GRANT USAGE, SELECT ON SEQUENCES TO {AppUsername};
        """;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("agendio")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-management-alpine")
        .WithUsername("agendio")
        .WithPassword("agendio_dev_only_test")
        .Build();

    // Sem builder tipado (Testcontainers nao publica um para MailHog): a API
    // generica de container cobre o caso, e e o unico jeito de testar o envio
    // de e-mail de convite contra um SMTP de verdade em vez de confiar so em
    // "o metodo foi chamado".
    private readonly IContainer _mailHog = new ContainerBuilder("mailhog/mailhog:latest")
        .WithPortBinding(1025, true)
        .WithPortBinding(8025, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/api/v2/messages")))
        .Build();

    private string OwnerConnectionString => BuildConnectionString(OwnerUsername, OwnerPassword);

    private string AppConnectionString => BuildConnectionString(AppUsername, AppPassword);

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync(), _rabbitMq.StartAsync(), _mailHog.StartAsync());

        await _postgres.ExecScriptAsync(BootstrapSql);

        // Migrations rodam como agendio_owner — a mesma role usada em produção
        // pela ferramenta `dotnet ef`, nunca a role de runtime da aplicação.
        await using (var tenancyDbContext = CreateTenancyDbContext())
        {
            await tenancyDbContext.Database.MigrateAsync();
        }

        await using (var identityDbContext = CreateIdentityDbContext())
        {
            await identityDbContext.Database.MigrateAsync();
        }

        await using (var customersDbContext = CreateCustomersDbContext())
        {
            await customersDbContext.Database.MigrateAsync();
        }

        await using (var catalogDbContext = CreateCatalogDbContext())
        {
            await catalogDbContext.Database.MigrateAsync();
        }

        await using (var resourcesDbContext = CreateResourcesDbContext())
        {
            await resourcesDbContext.Database.MigrateAsync();
        }

        await using (var schedulingDbContext = CreateSchedulingDbContext())
        {
            await schedulingDbContext.Database.MigrateAsync();
        }

        await using (var platformDbContext = CreatePlatformDbContext())
        {
            await platformDbContext.Database.MigrateAsync();
        }

        await using (var billingDbContext = CreateBillingDbContext())
        {
            await billingDbContext.Database.MigrateAsync();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
        await _rabbitMq.DisposeAsync();
        await _mailHog.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>Base da API REST do MailHog (GET /api/v2/messages) — usada pelos testes para confirmar entrega real.</summary>
    public string MailHogApiBaseUrl => $"http://{_mailHog.Hostname}:{_mailHog.GetMappedPublicPort(8025)}";

    /// <summary>
    /// Exposta so para testes que precisam ler a coluna CRUA (bypassando a API
    /// e o EF Core) — ex.: confirmar que um campo criptografado nao esta em
    /// texto plano no banco. Role owner de proposito: precisa enxergar a linha
    /// mesmo com RLS habilitada.
    /// </summary>
    public string DatabaseOwnerConnectionString => OwnerConnectionString;

    /// <summary>
    /// Role de runtime da aplicacao (agendio_app, NOBYPASSRLS) — usada por testes
    /// que precisam provar isolamento via Row Level Security de verdade, nao so
    /// via Global Query Filter do EF Core. Precisa de `SELECT set_config('app.tenant_id', ...)`
    /// apos abrir a conexao, mesmo comando do TenantConnectionInterceptor real.
    /// </summary>
    public string DatabaseAppConnectionString => AppConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting (nao ConfigureAppConfiguration) de proposito: o Program.cs
        // le "builder.Configuration" de forma sincrona logo apos WebApplication.
        // CreateBuilder(args), antes do Build(). ConfigureAppConfiguration so
        // aplicaria os overrides na configuracao final do host, tarde demais
        // para esse trecho. UseSetting vira argumento de linha de comando
        // repassado a Program.Main, que o CommandLineConfigurationProvider (uma
        // das fontes padrao de WebApplication.CreateBuilder) ja enxerga.
        var rabbitMqUri = new Uri(_rabbitMq.GetConnectionString());
        var rabbitMqUserInfo = rabbitMqUri.UserInfo.Split(':');

        builder.UseSetting("ConnectionStrings:Postgres", AppConnectionString);
        builder.UseSetting("ConnectionStrings:PostgresAdmin", OwnerConnectionString);
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        builder.UseSetting("RabbitMq:HostName", rabbitMqUri.Host);
        builder.UseSetting("RabbitMq:Port", rabbitMqUri.Port.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("RabbitMq:UserName", Uri.UnescapeDataString(rabbitMqUserInfo[0]));
        builder.UseSetting("RabbitMq:Password", Uri.UnescapeDataString(rabbitMqUserInfo[1]));
        builder.UseSetting("Jwt:Issuer", "https://agendio.test");
        builder.UseSetting("Jwt:Audience", "agendio-tests");
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-min-32-bytes-long!!");
        builder.UseSetting("PlatformJwt:Issuer", "https://agendio.test/platform");
        builder.UseSetting("PlatformJwt:Audience", "agendio-platform-tests");
        builder.UseSetting("PlatformJwt:SigningKey", "integration-test-platform-signing-key-min-32-bytes!!");
        builder.UseSetting("ColumnEncryption:Key", "aW50ZWdyYXRpb24tdGVzdC1jb2wta2V5LTMyYnl0ZXM=");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost");

        // O limite global de rate limiting e por IP, mas o TestServer nao tem
        // IP de cliente real — toda a suite roda numa unica particao "unknown".
        // Sem isso, o volume agregado de requisicoes de VARIOS testes (em
        // especial o teste de concorrencia do Scheduling, que sozinho manda 50)
        // no mesmo minuto derrubaria testes depois dele com 429, sem ser bug.
        builder.UseSetting("RateLimiting:GlobalPermitLimit", "100000");

        // Mesmo raciocinio para a politica "auth": agora cobre login/registro
        // do tenant tambem, e praticamente todo teste de integracao registra e
        // loga um tenant pelo menos uma vez — 10/min derrubaria a suite inteira
        // com 429. RateLimitingTests.cs usa builder.UseSetting proprio (valor
        // baixo) so nas classes que precisam exercitar o 429 de verdade.
        builder.UseSetting("RateLimiting:AuthPermitLimit", "100000");

        builder.UseSetting("Smtp:Host", _mailHog.Hostname);
        builder.UseSetting("Smtp:Port", _mailHog.GetMappedPublicPort(1025).ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("Smtp:FromAddress", "no-reply@agendio.test");
        builder.UseSetting("Frontend:BaseUrl", "http://localhost:3000");

        builder.UseSetting("Asaas:BaseUrl", "https://asaas.invalid/v3");
        builder.UseSetting("Asaas:ApiKey", "integration-test-fake-key");
        builder.UseSetting(AsaasWebhookSecretSettingKey, AsaasWebhookSecretForTests);

        // FakeAsaasClient no lugar do AsaasClient real: a Asaas e um servico
        // hospedado sem container local equivalente (ao contrario de
        // Postgres/Redis/RabbitMQ/MailHog acima) — nao da pra testar contra o
        // sandbox real em CI (precisaria de chave real e depende de rede
        // externa). O fluxo webhook-driven inteiro (assinar -> pagamento
        // pendente -> webhook confirma -> assinatura ativa) e testado igual,
        // so a chamada HTTP de saida e substituida por ids deterministicos.
        builder.ConfigureTestServices(services => services.AddSingleton<IAsaasClient, FakeAsaasClient>());
    }

    public const string AsaasWebhookSecretSettingKey = "Asaas:WebhookSecret";
    public const string AsaasWebhookSecretForTests = "integration-test-webhook-secret";

    private TenancyDbContext CreateTenancyDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseNpgsql(OwnerConnectionString)
            .UseSnakeCaseNamingConvention();

        return new TenancyDbContext(optionsBuilder.Options);
    }

    private IdentityDbContext CreateIdentityDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(OwnerConnectionString)
            .UseSnakeCaseNamingConvention();

        var encryptionService = new AesGcmEncryptionService(
            Options.Create(new ColumnEncryptionOptions { Key = "aW50ZWdyYXRpb24tdGVzdC1jb2wta2V5LTMyYnl0ZXM=" }));

        return new IdentityDbContext(optionsBuilder.Options, new NullTenantContext(), encryptionService);
    }

    private CustomersDbContext CreateCustomersDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseNpgsql(OwnerConnectionString)
            .UseSnakeCaseNamingConvention();

        // So roda MigrateAsync aqui (nunca criptografa/descriptografa nada de
        // verdade) — qualquer chave de 32 bytes serve, mesma usada em
        // ConfigureWebHost (ColumnEncryption:Key) para o host de teste real.
        var encryptionService = new AesGcmEncryptionService(
            Options.Create(new ColumnEncryptionOptions { Key = "aW50ZWdyYXRpb24tdGVzdC1jb2wta2V5LTMyYnl0ZXM=" }));

        return new CustomersDbContext(optionsBuilder.Options, new NullTenantContext(), encryptionService);
    }

    private CatalogDbContext CreateCatalogDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(OwnerConnectionString)
            .UseSnakeCaseNamingConvention();

        return new CatalogDbContext(optionsBuilder.Options, new NullTenantContext());
    }

    private ResourcesDbContext CreateResourcesDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ResourcesDbContext>()
            .UseNpgsql(OwnerConnectionString)
            .UseSnakeCaseNamingConvention();

        return new ResourcesDbContext(optionsBuilder.Options, new NullTenantContext());
    }

    private SchedulingDbContext CreateSchedulingDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<SchedulingDbContext>()
            .UseNpgsql(OwnerConnectionString)
            .UseSnakeCaseNamingConvention();

        return new SchedulingDbContext(optionsBuilder.Options, new NullTenantContext());
    }

    private PlatformDbContext CreatePlatformDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql(OwnerConnectionString)
            .UseSnakeCaseNamingConvention();

        return new PlatformDbContext(optionsBuilder.Options);
    }

    private BillingDbContext CreateBillingDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql(OwnerConnectionString)
            .UseSnakeCaseNamingConvention();

        return new BillingDbContext(optionsBuilder.Options);
    }

    private string BuildConnectionString(string username, string password)
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Username = username,
            Password = password,
        };

        return connectionStringBuilder.ConnectionString;
    }
}
