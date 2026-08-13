using Agendio.Modules.Tenancy.Domain;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantPageCustomization;

// Sem TenantId: vem de ITenantContext (claim do JWT), como em UpdateTenantProfileCommand.
public sealed record UpdateTenantPageCustomizationCommand(
    string? SecondaryColorHex,
    PublicPageFont Font,
    PublicPageButtonStyle ButtonStyle,
    bool ShowAboutSection,
    bool ShowServicesSection,
    bool ShowTeamSection,
    bool ShowHoursSection,
    bool ShowContactSection) : ICommand;
