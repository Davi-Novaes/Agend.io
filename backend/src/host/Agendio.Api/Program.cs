using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Agendio.Api;
using Agendio.Infrastructure.DependencyInjection;
using Agendio.Infrastructure.Endpoints;
using Agendio.Infrastructure.Messaging;
using Agendio.Infrastructure.Multitenancy;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Billing.DependencyInjection;
using Agendio.Modules.Billing.Infrastructure.Jobs;
using Agendio.Modules.Catalog.DependencyInjection;
using Agendio.Modules.Customers.DependencyInjection;
using Agendio.Modules.Estoque.DependencyInjection;
using Agendio.Modules.Assistant.DependencyInjection;
using Agendio.Modules.Marketing.DependencyInjection;
using Agendio.Modules.Financeiro.DependencyInjection;
using Agendio.Modules.Identity.DependencyInjection;
using Agendio.Modules.Platform.DependencyInjection;
using Agendio.Modules.Resources.DependencyInjection;
using Agendio.Modules.Scheduling.DependencyInjection;
using Agendio.Modules.Tenancy.DependencyInjection;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
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
    builder.Services.AddCustomersModule(builder.Configuration);
    builder.Services.AddCatalogModule(builder.Configuration);
    builder.Services.AddResourcesModule(builder.Configuration);
    builder.Services.AddSchedulingModule(builder.Configuration);
    builder.Services.AddPlatformModule(builder.Configuration);
    builder.Services.AddBillingModule(builder.Configuration);
    builder.Services.AddFinanceiroModule(builder.Configuration);
    builder.Services.AddEstoqueModule(builder.Configuration);
    builder.Services.AddMarketingModule(builder.Configuration);
    builder.Services.AddAssistantModule();
    builder.Services.AddAgendioHangfire(builder.Configuration);

    // ---------- Autenticacao / Autorizacao ----------
    // Duas autoridades JWT completamente separadas — issuer/audience/chave
    // proprios cada uma (ver PlatformJwtOptions): um token de tenant nunca
    // valida no scheme "Platform" e vice-versa, mesmo que alguem tente usar um
    // no lugar do outro. E assim que "Super Admin e autoridade separada, nunca
    // um papel dentro de tenant" (CLAUDE.md) vira garantia tecnica, nao so
    // promessa de codigo de aplicacao.
    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
        ?? throw new InvalidOperationException("Secao 'Jwt' nao configurada em appsettings.");
    var platformJwtOptions = builder.Configuration.GetSection(PlatformJwtOptions.SectionName).Get<PlatformJwtOptions>()
        ?? throw new InvalidOperationException("Secao 'PlatformJwt' nao configurada em appsettings.");

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
        })
        .AddJwtBearer(PlatformAuthConstants.AuthenticationScheme, options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = platformJwtOptions.Issuer,
                ValidAudience = platformJwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(platformJwtOptions.SigningKey)),
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(PlatformAuthConstants.AuthorizationPolicy, policy => policy
            .AddAuthenticationSchemes(PlatformAuthConstants.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireClaim(PlatformAuthConstants.ScopeClaimType, PlatformAuthConstants.PlatformScopeValue));
    });

    // ---------- Rate limiting ----------
    // Duas camadas: um limite generoso global, e um limite bem mais apertado
    // especificamente para login/registro (alvo classico de forca bruta e
    // enumeracao de contas). O limite global e configuravel porque o
    // WebApplicationFactory dos testes de integracao roda a suite inteira num
    // unico processo com uma unica particao "unknown" (TestServer nao tem IP
    // de cliente real) — sem isso, o volume agregado de requisicoes de VARIOS
    // testes no mesmo minuto derruba testes depois do teste de concorrencia
    // do Scheduling com 429, nao por bug, so por contencao do proprio teste.
    var globalRateLimitPermits = builder.Configuration.GetValue("RateLimiting:GlobalPermitLimit", 200);
    var globalRateLimitWindowSeconds = builder.Configuration.GetValue("RateLimiting:GlobalWindowSeconds", 60);

    // Configuravel pelo mesmo motivo do global acima: a partir de agora a
    // politica "auth" tambem cobre /api/auth/login e /api/auth/register do
    // tenant (nao so o login da Platform), e virtualmente todo teste de
    // integracao registra+loga um tenant — sem um limite alto nos testes essa
    // unica particao IP "unknown" estouraria 10/min ja nos primeiros testes.
    var authRateLimitPermits = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
    var authRateLimitWindowSeconds = builder.Configuration.GetValue("RateLimiting:AuthWindowSeconds", 60);

    // Fase 22 — Assistente: chamada de IA tem custo real por requisicao (chave
    // global paga pela plataforma), por isso um limite bem mais agressivo que o
    // global generico. Particiona por tenant (nao por IP) pelo mesmo motivo do
    // limite global acima.
    var aiAssistantRateLimitPermits = builder.Configuration.GetValue("RateLimiting:AiAssistantPermitLimit", 20);
    var aiAssistantRateLimitWindowSeconds = builder.Configuration.GetValue("RateLimiting:AiAssistantWindowSeconds", 3600);

    // Particiona por tenant (claim do JWT) quando a requisicao esta autenticada,
    // e cai para IP quando nao ha claim (login/registro/portal publico). Isso
    // exige que UseRateLimiter() rode DEPOIS de UseAuthentication() no pipeline
    // (ver abaixo) — antes disso HttpContext.User nunca tem a claim populada.
    static string ResolveGlobalRateLimitPartitionKey(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst(HttpTenantContext.TenantIdClaimType)?.Value;
        return !string.IsNullOrEmpty(tenantClaim)
            ? $"tenant:{tenantClaim}"
            : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ResolveGlobalRateLimitPartitionKey(httpContext),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = globalRateLimitPermits,
                    Window = TimeSpan.FromSeconds(globalRateLimitWindowSeconds),
                }));

        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authRateLimitPermits,
                    Window = TimeSpan.FromSeconds(authRateLimitWindowSeconds),
                }));

        options.AddPolicy("ai-assistant", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ResolveGlobalRateLimitPartitionKey(httpContext),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = aiAssistantRateLimitPermits,
                    Window = TimeSpan.FromSeconds(aiAssistantRateLimitWindowSeconds),
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

    // ---------- Outbox -> RabbitMQ (drena a cada minuto, por modulo) ----------
    app.Services.ScheduleOutboxProcessing<Agendio.Modules.Tenancy.Infrastructure.Persistence.TenancyDbContext>("tenancy");
    app.Services.ScheduleOutboxProcessing<Agendio.Modules.Identity.Infrastructure.Persistence.IdentityDbContext>("identity");
    app.Services.ScheduleOutboxProcessing<Agendio.Modules.Customers.Infrastructure.Persistence.CustomersDbContext>("customers");
    app.Services.ScheduleOutboxProcessing<Agendio.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext>("catalog");
    app.Services.ScheduleOutboxProcessing<Agendio.Modules.Resources.Infrastructure.Persistence.ResourcesDbContext>("resources");
    app.Services.ScheduleOutboxProcessing<Agendio.Modules.Scheduling.Infrastructure.Persistence.SchedulingDbContext>("scheduling");
    app.Services.ScheduleOutboxProcessing<Agendio.Modules.Platform.Infrastructure.Persistence.PlatformDbContext>("platform");
    app.Services.ScheduleOutboxProcessing<Agendio.Modules.Billing.Infrastructure.Persistence.BillingDbContext>("billing");
    app.Services.ScheduleOutboxProcessing<Agendio.Modules.Financeiro.Infrastructure.Persistence.FinanceiroDbContext>("financeiro");
    app.Services.ScheduleOutboxProcessing<Agendio.Modules.Estoque.Infrastructure.Persistence.EstoqueDbContext>("estoque");
    app.Services.ScheduleOutboxProcessing<Agendio.Modules.Marketing.Infrastructure.Persistence.MarketingDbContext>("marketing");

    // ---------- Conciliacao de assinaturas (diaria, ver Sprint 7) ----------
    // IRecurringJobManager resolvido do DI (nunca o facade estatico RecurringJob
    // — JobStorage.Current ainda nao esta inicializado logo apos app.Build(),
    // mesmo motivo documentado em HangfireServiceCollectionExtensions).
    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<BillingReconciliationJob>(
        "billing-reconciliation", job => job.RunAsync(CancellationToken.None), Cron.Daily(3));

    // ---------- Seed do primeiro Super Admin (Development apenas) ----------
    // Provisionamento de producao (rotacao de senha obrigatoria no primeiro
    // login, MFA, convite em vez de credencial fixa) fica fora do MVP de
    // proposito — ver ADR do Sprint 6. Em producao a secao "PlatformAdmin" nao
    // deveria nem existir na configuracao.
    if (app.Environment.IsDevelopment())
    {
        await Agendio.Modules.Platform.Seeding.PlatformAdminDevSeeder.SeedAsync(app.Services, app.Configuration);
    }

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

    // Serve o que LocalFileStorage grava em {ContentRoot}/uploads — logo do
    // tenant por enquanto. Nenhuma autorizacao aqui de proposito: e conteudo
    // publico (o mesmo logo que aparece no portal do cliente sem login).
    // PhysicalFileProvider exige que a pasta ja exista — cria antes de checkout
    // limpo, sem upload nenhum ainda ter acontecido.
    var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
    Directory.CreateDirectory(uploadsPath);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsPath),
        RequestPath = "/uploads",
    });

    app.UseCors("Default");

    // UseRateLimiter fica entre autenticacao e autorizacao de proposito:
    // UseAuthentication ja populou a claim tenant_id quando ha um JWT valido
    // (sem nunca rejeitar a requisicao — token ausente/invalido vira principal
    // anonimo, o fallback por IP da partition key acima), mas ainda precisa
    // rodar ANTES de UseAuthorization para que um flood sem token valido contra
    // rota protegida continue consumindo o limiter em vez de ser barrado por
    // 401 antes de chegar aqui (senao reabre o gap de DoS que a ordem antiga
    // — limiter antes de tudo — evitava).
    app.UseAuthentication();
    app.UseRateLimiter();
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
