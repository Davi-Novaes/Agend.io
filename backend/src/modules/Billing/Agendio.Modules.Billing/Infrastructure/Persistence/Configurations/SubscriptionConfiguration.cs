using Agendio.Modules.Billing.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Billing.Infrastructure.Persistence.Configurations;

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasConversion(id => id.Value, value => SubscriptionId.From(value)).ValueGeneratedNever();

        // Coluna comum, SEM ITenantOwned/RLS de proposito — ver comentario em Subscription.cs.
        builder.Property(s => s.TenantId).HasConversion(id => id.Value, value => TenantId.From(value)).IsRequired();

        builder.Property(s => s.PlanId).HasConversion(id => id.Value, value => PlanId.From(value)).IsRequired();

        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.AsaasCustomerId).HasMaxLength(64);
        builder.Property(s => s.AsaasSubscriptionId).HasMaxLength(64);
        builder.Property(s => s.CreatedBy).HasMaxLength(256);
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);

        // Unico de proposito: existe EXATAMENTE uma Subscription por tenant durante
        // toda a vida dele (o trial e a assinatura paga sao a MESMA linha, so o
        // Status muda) — nunca criamos uma segunda ao assinar/cancelar/reativar.
        // Protege contra duas linhas nascendo se o consumidor de evento e o
        // auto-heal de GetMySubscriptionQueryHandler correrem em paralelo.
        builder.HasIndex(s => s.TenantId).IsUnique();
    }
}
