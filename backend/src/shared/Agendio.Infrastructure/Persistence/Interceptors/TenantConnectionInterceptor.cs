using System.Data.Common;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Agendio.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Terceira camada da defesa em profundidade multi-tenant (as outras duas sao o
/// claim do JWT resolvido pelo middleware e o Global Query Filter do EF Core).
///
/// A cada abertura logica de conexao — inclusive reaproveitando uma conexao do
/// pool do Npgsql — executa `SET app.tenant_id`, a variavel de sessao que as
/// politicas de Row Level Security do PostgreSQL leem via current_setting().
/// A aplicacao conecta com uma role SEM BYPASSRLS: mesmo que o Global Query
/// Filter falhe por algum motivo (bug, query com IgnoreQueryFilters por engano),
/// o banco em si recusa devolver linha de outro tenant.
/// </summary>
public sealed class TenantConnectionInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetTenantGuc(connection, CancellationToken.None).GetAwaiter().GetResult();
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetTenantGuc(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private async Task SetTenantGuc(DbConnection connection, CancellationToken cancellationToken)
    {
        var tenantIdValue = tenantContext.HasTenant ? tenantContext.TenantId.Value.ToString() : Guid.Empty.ToString();

        var command = connection.CreateCommand();
        await using (command)
        {
            // set_config com parametro (nunca interpolacao de string) elimina
            // qualquer risco de SQL injection nesta variavel de sessao.
            command.CommandText = "SELECT set_config('app.tenant_id', @tenantId, false)";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "tenantId";
            parameter.Value = tenantIdValue;
            command.Parameters.Add(parameter);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
