const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5071";

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly code?: string
  ) {
    super(message);
    this.name = "ApiError";
  }
}

// Formato ProblemDetails (RFC 9457) devolvido por Error.ToProblemResult() no backend.
type ProblemDetails = {
  title?: string;
  detail?: string;
  status?: number;
  code?: string;
};

async function request<TResponse>(
  path: string,
  options: RequestInit = {},
  accessToken?: string
): Promise<TResponse> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    // Necessario para o cookie HttpOnly do refresh token (definido em /api/auth/*).
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      ...options.headers,
    },
  });

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as ProblemDetails | null;
    throw new ApiError(
      problem?.detail ?? problem?.title ?? "Ocorreu um erro inesperado.",
      response.status,
      problem?.code
    );
  }

  if (response.status === 204) {
    return undefined as TResponse;
  }

  return (await response.json()) as TResponse;
}

export type TenantSummary = {
  id: string;
  name: string;
  slug: string;
  isActive: boolean;
  primaryColorHex: string | null;
};

export function getTenantBySlug(slug: string): Promise<TenantSummary> {
  return request<TenantSummary>(`/api/tenants/by-slug/${encodeURIComponent(slug)}`);
}

export type TerminologyPack = {
  customer: string;
  customerPlural: string;
  service: string;
  servicePlural: string;
  staff: string;
  staffPlural: string;
  appointment: string;
};

export type BusinessTypeOption = {
  value: string;
  displayName: string;
  terminology: TerminologyPack;
};

export function listBusinessTypes(): Promise<BusinessTypeOption[]> {
  return request<BusinessTypeOption[]>("/api/tenants/business-types");
}

export function createTenant(input: {
  name: string;
  slug: string;
  businessType: string;
  timeZoneId: string;
}): Promise<{ id: string }> {
  return request("/api/tenants", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export type AuthTokens = {
  accessToken: string;
  expiresAtUtc: string;
};

export function login(input: {
  tenantId: string;
  email: string;
  password: string;
}): Promise<AuthTokens> {
  return request<AuthTokens>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function registerUser(input: {
  tenantId: string;
  email: string;
  password: string;
  fullName: string;
}): Promise<{ id: string }> {
  return request("/api/auth/register", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function refreshAccessToken(): Promise<AuthTokens> {
  return request<AuthTokens>("/api/auth/refresh", { method: "POST" });
}

export function updateTenantBranding(primaryColorHex: string, accessToken: string): Promise<void> {
  return request<void>(
    "/api/tenants/branding",
    { method: "PUT", body: JSON.stringify({ primaryColorHex }) },
    accessToken
  );
}

export type TeamMember = {
  id: string;
  email: string;
  fullName: string;
  role: string;
  isActive: boolean;
};

export function listTeamMembers(accessToken: string): Promise<TeamMember[]> {
  return request<TeamMember[]>("/api/team/members", {}, accessToken);
}

export type PendingInvitation = {
  id: string;
  email: string;
  role: string;
  expiresAtUtc: string;
};

export function listPendingInvitations(accessToken: string): Promise<PendingInvitation[]> {
  return request<PendingInvitation[]>("/api/team/invitations", {}, accessToken);
}

export type InviteTeamMemberResult = {
  invitationId: string;
  token: string;
  expiresAtUtc: string;
};

export function inviteTeamMember(
  input: { email: string; role: string },
  accessToken: string
): Promise<InviteTeamMemberResult> {
  return request<InviteTeamMemberResult>(
    "/api/team/invitations",
    { method: "POST", body: JSON.stringify(input) },
    accessToken
  );
}

export function acceptInvitation(input: {
  token: string;
  fullName: string;
  password: string;
}): Promise<{ id: string }> {
  return request(`/api/team/invitations/${encodeURIComponent(input.token)}/accept`, {
    method: "POST",
    body: JSON.stringify({ fullName: input.fullName, password: input.password }),
  });
}
