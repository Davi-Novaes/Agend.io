using Agendio.Modules.Financeiro.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Financeiro.Infrastructure.Persistence.Configurations;

public sealed class CommissionRuleConfiguration : IEntityTypeConfiguration<CommissionRule>
{
    public void Configure(EntityTypeBuilder<CommissionRule> builder)
    {
        builder.ToTable("commission_rules");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => CommissionRuleId.From(value))
            .ValueGeneratedNever();

        builder.Property(c => c.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(c => c.ResourceId).IsRequired();
        builder.Property(c => c.CalculationType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.Value).HasColumnType("numeric(10,2)").IsRequired();
        builder.Property(c => c.IsActive).IsRequired();

        // Uma regra por profissional (decisao de produto: sem granularidade por servico).
        builder.HasIndex(c => new { c.TenantId, c.ResourceId }).IsUnique();

        // Filtro de tenant fica no DbContext (ver AgendioDbContextBase).
    }
}
