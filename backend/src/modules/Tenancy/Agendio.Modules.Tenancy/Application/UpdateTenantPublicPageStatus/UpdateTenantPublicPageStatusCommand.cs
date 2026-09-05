using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantPublicPageStatus;

// Sem TenantId: vem de ITenantContext (claim do JWT). Distinto de qualquer
// comando de (des)ativacao de tenant do Super Admin — este so publica/
// despublica a pagina publica ([slug]), nunca afeta login nem cobranca.
public sealed record UpdateTenantPublicPageStatusCommand(bool Enabled) : ICommand;
