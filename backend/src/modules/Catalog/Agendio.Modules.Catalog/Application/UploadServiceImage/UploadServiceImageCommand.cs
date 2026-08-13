using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Catalog.Application.UploadServiceImage;

public sealed record UploadServiceImageCommand(Guid ServiceId, byte[] Content, string ContentType) : ICommand<string>;
