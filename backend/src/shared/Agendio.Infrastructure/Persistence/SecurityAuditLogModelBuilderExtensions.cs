using Microsoft.EntityFrameworkCore;

namespace Agendio.Infrastructure.Persistence;

/// <summary>Configuracao compartilhada da tabela security_audit_log — chamada pelo OnModelCreating de cada modulo que autentica identidade.</summary>
public static class SecurityAuditLogModelBuilderExtensions
{
    public static void ConfigureSecurityAuditLog(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SecurityAuditLogEntry>(builder =>
        {
            builder.ToTable("security_audit_log");
            builder.HasKey(entry => entry.Id);
            builder.Property(entry => entry.EventType).IsRequired().HasMaxLength(64);
            builder.Property(entry => entry.IpAddress).HasMaxLength(64);
            builder.Property(entry => entry.CountryCode).HasMaxLength(2);
            builder.Property(entry => entry.Region).HasMaxLength(100);
            builder.Property(entry => entry.City).HasMaxLength(100);
            builder.Property(entry => entry.UserAgent).HasMaxLength(512);
            builder.Property(entry => entry.Metadata).HasColumnType("jsonb");
            builder.HasIndex(entry => new { entry.ActorId, entry.OccurredAtUtc });
            builder.HasIndex(entry => new { entry.TenantId, entry.OccurredAtUtc });
            builder.HasIndex(entry => entry.EventType);
        });
    }
}
