using System.Globalization;
using Agendio.Infrastructure.Multitenancy;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

    private string OwnerConnectionString => BuildConnectionString(OwnerUsername, OwnerPassword);

    private string AppConnectionString => BuildConnectionString(AppUsername, AppPassword);

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync(), _rabbitMq.StartAsync());

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
    }

    public override async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
        await _rabbitMq.DisposeAsync();
        await base.DisposeAsync();
    }

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
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost");
    }

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

        return new IdentityDbContext(optionsBuilder.Options, new NullTenantContext());
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
