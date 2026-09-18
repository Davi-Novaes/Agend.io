using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Identity.Application.UpdateMyProfile;

/// <summary>UserId vem da claim do JWT — sem checagem de role, o usuario so edita o proprio registro (mesmo padrao de UploadUserAvatarCommand).</summary>
public sealed record UpdateMyProfileCommand(Guid UserId, string? FullName, string? Phone) : ICommand;
