using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantCompanyInfo;

// Sem TenantId: vem de ITenantContext (claim do JWT), como em UpdateTenantProfileCommand.
// Dado cadastral (razao social/documento) — nunca exposto no perfil publico
// (GetPublicTenantProfileQuery), so no GetTenantProfileQuery administrativo.
public sealed record UpdateTenantCompanyInfoCommand(
    string Name, string? LegalName, string? Document, string? City, string? State, string? ZipCode) : ICommand;
