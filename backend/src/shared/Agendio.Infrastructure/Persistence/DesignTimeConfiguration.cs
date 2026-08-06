using Microsoft.Extensions.Configuration;

namespace Agendio.Infrastructure.Persistence;

/// <summary>
/// Carrega configuracao para as IDesignTimeDbContextFactory de cada modulo,
/// usadas exclusivamente pela ferramenta `dotnet ef`. Pressupoe que o comando e
/// executado com `--startup-project src/host/Agendio.Api` (ou a partir daquele
/// diretorio) — e assim que o `dotnet-ef` define o diretorio de trabalho ao
/// invocar a factory, e e o unico lugar onde os appsettings.*.json vivem.
/// </summary>
public static class DesignTimeConfiguration
{
    public static IConfiguration Build()
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";

        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }
}
