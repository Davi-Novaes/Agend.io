using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Identity.Application.UploadUserAvatar;

public sealed record UploadUserAvatarCommand(Guid UserId, byte[] Content, string ContentType) : ICommand<string>;
