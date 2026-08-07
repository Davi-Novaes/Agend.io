using Agendio.Modules.Billing.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Billing.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion(id => id.Value, value => PaymentId.From(value)).ValueGeneratedNever();

        // Coluna comum, SEM ITenantOwned/RLS de proposito — ver comentario em Subscription.cs.
        builder.Property(p => p.TenantId).HasConversion(id => id.Value, value => TenantId.From(value)).IsRequired();

        builder.Property(p => p.SubscriptionId).HasConversion(id => id.Value, value => SubscriptionId.From(value)).IsRequired();

        builder.Property(p => p.AsaasPaymentId).IsRequired().HasMaxLength(64);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Amount).HasColumnType("numeric(10,2)");
        builder.Property(p => p.InvoiceUrl).HasMaxLength(2048);
        builder.Property(p => p.BillingType).IsRequired().HasMaxLength(20);
        builder.Property(p => p.CreatedBy).HasMaxLength(256);
        builder.Property(p => p.UpdatedBy).HasMaxLength(256);

        // Chave de idempotencia do webhook — ver comentario em Payment.cs.
        builder.HasIndex(p => p.AsaasPaymentId).IsUnique();
        builder.HasIndex(p => p.TenantId);
    }
}
