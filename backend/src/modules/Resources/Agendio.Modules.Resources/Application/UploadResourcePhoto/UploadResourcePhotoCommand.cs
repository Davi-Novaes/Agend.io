using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Resources.Application.UploadResourcePhoto;

public sealed record UploadResourcePhotoCommand(Guid ResourceId, byte[] Content, string ContentType) : ICommand<string>;
