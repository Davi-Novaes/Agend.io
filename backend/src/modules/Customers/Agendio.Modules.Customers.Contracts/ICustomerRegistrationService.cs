using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Customers.Contracts;

/// <summary>
/// Excecao deliberada a regra "Contracts so expoe leitura sincrona" (ver
/// CLAUDE.md): o portal publico de agendamento precisa resolver-ou-criar o
/// cliente e criar o agendamento na MESMA requisicao — nao ha tempo (nem
/// necessidade real) de coreografar isso via integration event assincrono.
/// A escrita continua isolada dentro do modulo Customers, atras da sua
/// propria logica de dominio — Scheduling nunca toca CustomersDbContext.
/// </summary>
public interface ICustomerRegistrationService
{
    /// <summary>
    /// Reaproveita um cliente existente pelo e-mail (case-insensitive) ou cria
    /// um novo. cpf so e usado na CRIACAO (Fase 16, cobranca de sinal) — um
    /// cliente ja existente sem CPF nao e retroativamente atualizado aqui.
    /// </summary>
    Task<Result<Guid>> FindOrRegisterByEmailAsync(
        string fullName, string email, string? phone, string? cpf = null, CancellationToken cancellationToken = default);
}
