using Agendio.Modules.Tenancy.Domain;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Tenancy.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .ValueGeneratedNever();

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);

        builder.Property(t => t.Slug)
            .HasConversion(slug => slug.Value, value => Slug.Create(value).Value)
            .HasMaxLength(63)
            .IsRequired();
        builder.HasIndex(t => t.Slug).IsUnique();

        // Default de banco (nao so na aplicacao) para tenants que ja existiam
        // antes desta coluna: ficam classificados como "Other" em vez de
        // quebrar a migration.
        builder.Property(t => t.BusinessType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue(BusinessType.Other);

        builder.Property(t => t.TimeZoneId).IsRequired().HasMaxLength(64);
        builder.Property(t => t.IsActive).IsRequired();
        builder.Property(t => t.PrimaryColorHex).HasMaxLength(7);
        builder.Property(t => t.LogoUrl).HasMaxLength(500);
        builder.Property(t => t.BannerUrl).HasMaxLength(500);

        builder.Property(t => t.Description).HasMaxLength(2000);

        builder.Property(t => t.Phone)
            .HasConversion(
                phone => phone == null ? null : phone.Value,
                value => value == null ? null : PhoneNumber.Create(value).Value)
            .HasMaxLength(20);

        builder.Property(t => t.WhatsApp)
            .HasConversion(
                phone => phone == null ? null : phone.Value,
                value => value == null ? null : PhoneNumber.Create(value).Value)
            .HasMaxLength(20);

        builder.Property(t => t.Email)
            .HasConversion(
                email => email == null ? null : email.Value,
                value => value == null ? null : Email.Create(value).Value)
            .HasMaxLength(320);

        builder.Property(t => t.Address).HasMaxLength(500);
        builder.Property(t => t.InstagramUrl).HasMaxLength(500);
        builder.Property(t => t.FacebookUrl).HasMaxLength(500);

        builder.Property(t => t.SecondaryColorHex).HasMaxLength(7);

        builder.Property(t => t.Font)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValue(PublicPageFont.Default);

        builder.Property(t => t.ButtonStyle)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(PublicPageButtonStyle.Rounded);

        builder.Property(t => t.ShowAboutSection).IsRequired().HasDefaultValue(true);
        builder.Property(t => t.ShowServicesSection).IsRequired().HasDefaultValue(true);
        builder.Property(t => t.ShowTeamSection).IsRequired().HasDefaultValue(true);
        builder.Property(t => t.ShowHoursSection).IsRequired().HasDefaultValue(true);
        builder.Property(t => t.ShowContactSection).IsRequired().HasDefaultValue(true);

        builder.Property(t => t.AppointmentBufferMinutes).IsRequired().HasDefaultValue(0);

        builder.Property(t => t.WhatsAppIntegrationEnabled).IsRequired().HasDefaultValue(false);
        builder.Property(t => t.WhatsAppPhoneNumberId).HasMaxLength(64);
        // WhatsAppAccessToken: sem HasMaxLength de proposito, mesmo raciocinio
        // de Customer.Cpf/HealthNotes — o conversor criptografado (aplicado no
        // OnModelCreating do TenancyDbContext, depois deste IEntityTypeConfiguration
        // rodar) aumenta o tamanho do valor persistido.
        builder.Property(t => t.WhatsAppScheduledTemplate).HasMaxLength(1000);
        builder.Property(t => t.WhatsAppReminderTemplate).HasMaxLength(1000);
        builder.Property(t => t.WhatsAppCancelledTemplate).HasMaxLength(1000);
        builder.Property(t => t.WhatsAppRescheduledTemplate).HasMaxLength(1000);
        builder.Property(t => t.WhatsAppConfirmedTemplate).HasMaxLength(1000);
        builder.Property(t => t.WhatsAppCompletedTemplate).HasMaxLength(1000);

        builder.Property(t => t.Reminder24hEnabled).IsRequired().HasDefaultValue(true);
        builder.Property(t => t.Reminder2hEnabled).IsRequired().HasDefaultValue(true);
        builder.Property(t => t.PostServiceThankYouEnabled).IsRequired().HasDefaultValue(true);

        builder.Property(t => t.LoyaltyProgramEnabled).IsRequired().HasDefaultValue(true);
        builder.Property(t => t.LoyaltyVisitsForReward).IsRequired().HasDefaultValue(10);
        builder.Property(t => t.LoyaltyRewardDescription).IsRequired().HasMaxLength(200).HasDefaultValue("Um agradecimento especial");

        builder.Property(t => t.RequireDepositAfterNoShows).IsRequired().HasDefaultValue(false);
        builder.Property(t => t.NoShowThresholdForDeposit).IsRequired().HasDefaultValue(2);

        builder.Property(t => t.PaymentRequirement)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(PaymentRequirement.None);
        builder.Property(t => t.DepositPercentage).IsRequired().HasDefaultValue(30);

        // Colecao de Value Objects sem identidade propria — tabela filha, FK
        // sombra gerada pelo EF, sem repositorio separado (mesmo padrao de
        // Resource.WorkingHours no modulo Resources).
        builder.OwnsMany(t => t.BusinessHours, businessHours =>
        {
            businessHours.ToTable("tenant_business_hours");
            businessHours.WithOwner().HasForeignKey("tenant_id");
            businessHours.Property<int>("id");
            businessHours.HasKey("id");

            businessHours.Property(h => h.DayOfWeek).HasConversion<string>().HasMaxLength(20).IsRequired();
            businessHours.Property(h => h.StartTime).IsRequired();
            businessHours.Property(h => h.EndTime).IsRequired();
        });

        builder.OwnsMany(t => t.ClosedDates, closedDates =>
        {
            closedDates.ToTable("tenant_closed_dates");
            closedDates.WithOwner().HasForeignKey("tenant_id");
            closedDates.Property<int>("id");
            closedDates.HasKey("id");

            closedDates.Property(d => d.Date).IsRequired();
            closedDates.Property(d => d.Reason).HasMaxLength(200);
        });

        builder.Property(t => t.CreatedBy).HasMaxLength(256);
        builder.Property(t => t.UpdatedBy).HasMaxLength(256);

        // Tenant nao e ITenantOwned (nao ha RLS por TenantId aqui), mas ainda
        // esconde registros desativados via soft-delete.
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
