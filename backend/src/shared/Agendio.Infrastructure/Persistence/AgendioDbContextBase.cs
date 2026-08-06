using Microsoft.EntityFrameworkCore;

namespace Agendio.Infrastructure.Persistence;

/// <summary>
/// Base compartilhada por todo DbContext de modulo. Cada modulo (Identity,
/// Tenancy, e futuramente Scheduling, Catalog...) declara o PROPRIO DbContext
/// derivado desta classe, com as PROPRIAS tabelas — nunca um DbContext unico
/// conhecendo entidades de todos os modulos.
///
/// Por que um DbContext por modulo, e nao um so para o backend inteiro: a regra
/// "nenhum modulo acessa tabela de outro modulo" so e verificavel de verdade se
/// cada modulo literalmente nao enxerga o DbSet do outro. Um DbContext unico
/// exigiria que Agendio.Infrastructure (referenciada por todos) tivesse
/// referencia de volta para cada modulo — dependencia circular. Varios
/// DbContext apontando para o MESMO banco fisico resolve isso sem ciclo.
///
/// ARMADILHA ao configurar entidade ITenantOwned num modulo novo: HasQueryFilter
/// SOBRESCREVE (nao combina) um filtro ja definido na mesma entidade. Se a
/// IEntityTypeConfiguration da entidade define soft-delete (`!x.IsDeleted`) e o
/// DbContext depois define o filtro de tenant separadamente, um apaga o outro.
/// Sempre combine tudo numa unica expressao no OnModelCreating do DbContext, ex.:
/// <c>modelBuilder.Entity&lt;X&gt;().HasQueryFilter(x => x.TenantId == CurrentTenantId() &amp;&amp; !x.IsDeleted);</c>
/// — nunca configure HasQueryFilter dentro da IEntityTypeConfiguration de uma
/// entidade ITenantOwned (ver Agendio.Modules.Identity.Infrastructure.Persistence.IdentityDbContext).
/// </summary>
public abstract class AgendioDbContextBase(DbContextOptions options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("outbox_messages");
            builder.HasKey(message => message.Id);
            builder.Property(message => message.Type).IsRequired().HasMaxLength(1024);
            builder.Property(message => message.Content).IsRequired().HasColumnType("jsonb");
            builder.Property(message => message.Error).HasMaxLength(2048);
            builder.HasIndex(message => message.ProcessedOnUtc);
        });

        base.OnModelCreating(modelBuilder);
    }
}
