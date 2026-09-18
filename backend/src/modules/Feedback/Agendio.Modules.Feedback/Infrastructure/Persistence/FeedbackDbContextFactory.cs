using Agendio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Agendio.Modules.Feedback.Infrastructure.Persistence;

/// <summary>Usada exclusivamente pela ferramenta `dotnet ef` — nunca pelo container de DI em runtime.</summary>
public sealed class FeedbackDbContextFactory : IDesignTimeDbContextFactory<FeedbackDbContext>
{
    public FeedbackDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfiguration.Build();
        var connectionString = configuration.GetConnectionString("PostgresAdmin")
            ?? throw new InvalidOperationException("Connection string 'PostgresAdmin' nao configurada para design-time.");

        var optionsBuilder = new DbContextOptionsBuilder<FeedbackDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new FeedbackDbContext(optionsBuilder.Options);
    }
}
