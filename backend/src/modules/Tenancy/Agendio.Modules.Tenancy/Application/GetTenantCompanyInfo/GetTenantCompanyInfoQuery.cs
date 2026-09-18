using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.GetTenantCompanyInfo;

public sealed record GetTenantCompanyInfoQuery : IQuery<TenantCompanyInfo>;

/// <summary>
/// Dado cadastral do estabelecimento (nunca exibido na pagina publica) —
/// separado de TenantProfile de proposito: aquele e lido por QUALQUER papel
/// autenticado (Sidebar/Header/TenantThemeProvider precisam dele), mas
/// CNPJ/CPF/razao social/localizacao cadastral so o Owner deve ver (pedido
/// explicito do usuario, 2026-09-05) — por isso este e um endpoint Owner-only
/// a parte, nao so mais um campo condicional no DTO grande.
/// </summary>
public sealed record TenantCompanyInfo(
    string Name,
    string? LegalName,
    string? Document,
    string? City,
    string? State,
    string? ZipCode);
