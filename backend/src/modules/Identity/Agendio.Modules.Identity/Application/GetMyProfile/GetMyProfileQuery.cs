using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Identity.Application.GetMyProfile;

/// <summary>UserId vem da claim do JWT. Usado pelo AppHeader pra mostrar a foto de perfil (nao entra no token — mudaria a cada re-upload sem precisar de um token novo).</summary>
public sealed record GetMyProfileQuery(Guid UserId) : IQuery<MyProfileResult>;

public sealed record MyProfileResult(string Email, string FullName, string? AvatarUrl, string? Phone);
