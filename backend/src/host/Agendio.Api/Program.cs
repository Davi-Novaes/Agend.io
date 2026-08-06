using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Agendio.Api;
using Agendio.Infrastructure.DependencyInjection;
using Agendio.Infrastructure.Endpoints;
using Agendio.Infrastructure.Messaging;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.DependencyInjection;
using Agendio.Modules.Tenancy.DependencyInjection;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using Scalar.AspNetCore;
using Serilog;

// Logger de "bootstrap": captura falha de configuracao ANTES do host terminar
// de subir (ex.: erro ao ler appsettings) — sem isso, esse tipo de falha vira
// stack trace cru no console em vez de log estruturado.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    Log.Information("Iniciando Agendio.Api");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

    // ---------- Observabilidade (OpenTelemetry) ----------
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("Agendio.Api"))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter())
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter());

    // ---------- Infraestrutura transversal + modulos ----------
    builder.Services.AddAgendioInfrastructure(builder.Configuration);
    builder.Services.AddTenancyModule(builder.Configuration);
    builder.Services.AddIdentityModule(builder.Configuration);
    builder.Services.AddAgendioHangfire(builder.Configuration);

    // ---------- Autenticacao / Autorizacao ----------
    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
        ?? throw new InvalidOperationException("Secao 'Jwt' nao configurada em appsettings.");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });

    builder.Services.AddAuthorization();

    // ---------- Rate limiting ----------
    // Duas camadas: um limite generoso global, e um limite bem mais apertado
    // especificamente para login/registro (alvo classico de forca bruta e
    // enumeracao de contas).
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 200,
                    Window = TimeSpan.FromMinutes(1),
                }));

        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                }));
    });

    // ---------- CORS ----------
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Default", policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            // Necessario para o cookie HttpOnly do refresh token viajar em
            // requisicoes cross-origin (frontend em outra porta/dominio).
            .AllowCredentials());
    });

    // Enums trafegam como nome ("Barbershop"), nao como numero — o frontend
    // (e quem le o log/Swagger) nao deveria precisar saber a ordem declarada.
    builder.Services.ConfigureHttpJsonOptions(options =>
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    // ---------- OpenAPI ----------
    builder.Services.AddOpenApi();

    // ---------- Health checks ----------
    builder.Services.AddHealthChecks()
        .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!, name: "postgres", tags: ["ready"])
        .AddRedis(sp => sp.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>(), name: "redis", tags: ["ready"])
        .AddRabbitMQ(
            sp =>
            {
                var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
                var factory = new ConnectionFactory
                {
                    HostName = options.HostName,
                    Port = options.Port,
                    UserName = options.UserName,
                    Password = options.Password,
                };
                return factory.CreateConnectionAsync();
            },
            name: "rabbitmq",
            tags: ["ready"]);

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    // ---------- Cabecalhos de seguranca ----------
    // CSP restritiva de proposito: a API so serve JSON, nunca HTML renderizado
    // com script de terceiro — "default-src 'none'" e seguro aqui (diferente do
    // frontend Next.js, que precisa de uma CSP propria e mais permissiva).
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
        await next();
    });

    app.UseHttpsRedirection();

    app.UseCors("Default");
    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    // /health/live: o processo esta de pe (nao verifica dependencia externa —
    // usado por orquestrador para decidir se precisa reiniciar o container).
    // /health/ready: pronto para receber trafego real (Postgres/Redis/RabbitMQ acessiveis).
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireDashboardAuthorizationFilter()],
    });

    foreach (var endpointModule in app.Services.GetServices<IEndpointModule>())
    {
        endpointModule.MapEndpoints(app);
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Agendio.Api encerrado de forma inesperada durante o startup");
}
finally
{
    Log.CloseAndFlush();
}

// Necessario para WebApplicationFactory<Program> nos testes de integracao
// (Agendio.IntegrationTests) enxergar este assembly como ponto de entrada.
public partial class Program;
