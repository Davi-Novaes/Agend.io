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

export type TenantPublicProfile = {
  id: string;
  name: string;
  slug: string;
  isActive: boolean;
  primaryColorHex: string | null;
  logoUrl: string | null;
  bannerUrl: string | null;
  description: string | null;
  phone: string | null;
  whatsApp: string | null;
  email: string | null;
  address: string | null;
  instagramUrl: string | null;
  facebookUrl: string | null;
  secondaryColorHex: string | null;
  font: PublicPageFont;
  buttonStyle: PublicPageButtonStyle;
  showAboutSection: boolean;
  showServicesSection: boolean;
  showTeamSection: boolean;
  showHoursSection: boolean;
  showContactSection: boolean;
  businessHours: WorkingHourEntry[];
};

/** Resolve um logoUrl relativo (ex.: "/uploads/tenant-logos/x.png") para a origem da API. */
export function resolveAssetUrl(path: string): string {
  return `${API_BASE_URL}${path}`;
}

export function getTenantBySlug(slug: string): Promise<TenantPublicProfile> {
  return request<TenantPublicProfile>(`/api/tenants/by-slug/${encodeURIComponent(slug)}`);
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
  mfaRequired: false;
  accessToken: string;
  expiresAtUtc: string;
};

export type MfaChallenge = {
  mfaRequired: true;
  mfaChallengeToken: string;
  expiresAtUtc: string;
};

// /login e /mfa/verify devolvem o mesmo formato de uniao: ou tokens de
// verdade (mfaRequired: false), ou um desafio pendente que precisa de um
// segundo passo antes de autenticar de fato.
export type LoginResult = AuthTokens | MfaChallenge;

export function login(input: {
  tenantId: string;
  email: string;
  password: string;
}): Promise<LoginResult> {
  return request<LoginResult>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function verifyMfa(input: { mfaChallengeToken: string; code: string }): Promise<AuthTokens> {
  return request<AuthTokens>("/api/auth/mfa/verify", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export type SetupMfaResult = { secret: string; otpAuthUri: string };

export function setupMfa(accessToken: string): Promise<SetupMfaResult> {
  return request<SetupMfaResult>("/api/auth/mfa/setup", { method: "POST" }, accessToken);
}

export function enableMfa(
  input: { secret: string; code: string },
  accessToken: string
): Promise<{ recoveryCodes: string[] }> {
  return request("/api/auth/mfa/enable", { method: "POST", body: JSON.stringify(input) }, accessToken);
}

export function disableMfa(input: { password: string; code: string }, accessToken: string): Promise<void> {
  return request<void>("/api/auth/mfa/disable", { method: "POST", body: JSON.stringify(input) }, accessToken);
}

export function getMfaStatus(accessToken: string): Promise<{ mfaEnabled: boolean }> {
  return request("/api/auth/mfa/status", {}, accessToken);
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

export async function uploadTenantLogo(file: File, accessToken: string): Promise<{ logoUrl: string }> {
  const formData = new FormData();
  formData.append("file", file);

  // Nao usa request(): FormData precisa que o browser defina o Content-Type
  // (com o boundary do multipart) sozinho — setar "application/json" quebraria o upload.
  const response = await fetch(`${API_BASE_URL}/api/tenants/logo`, {
    method: "POST",
    credentials: "include",
    headers: { Authorization: `Bearer ${accessToken}` },
    body: formData,
  });

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as ProblemDetails | null;
    throw new ApiError(
      problem?.detail ?? problem?.title ?? "Nao foi possivel enviar o logo.",
      response.status,
      problem?.code
    );
  }

  return response.json();
}

export async function uploadTenantBanner(file: File, accessToken: string): Promise<{ bannerUrl: string }> {
  const formData = new FormData();
  formData.append("file", file);

  const response = await fetch(`${API_BASE_URL}/api/tenants/banner`, {
    method: "POST",
    credentials: "include",
    headers: { Authorization: `Bearer ${accessToken}` },
    body: formData,
  });

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as ProblemDetails | null;
    throw new ApiError(
      problem?.detail ?? problem?.title ?? "Nao foi possivel enviar o banner.",
      response.status,
      problem?.code
    );
  }

  return response.json();
}

export type PublicPageFont = "Default" | "Poppins" | "PlayfairDisplay" | "Merriweather";

export type PublicPageButtonStyle = "Rounded" | "Square" | "Pill";

export type TenantProfile = {
  name: string;
  slug: string;
  primaryColorHex: string | null;
  logoUrl: string | null;
  bannerUrl: string | null;
  description: string | null;
  phone: string | null;
  whatsApp: string | null;
  email: string | null;
  address: string | null;
  instagramUrl: string | null;
  facebookUrl: string | null;
  secondaryColorHex: string | null;
  font: PublicPageFont;
  buttonStyle: PublicPageButtonStyle;
  showAboutSection: boolean;
  showServicesSection: boolean;
  showTeamSection: boolean;
  showHoursSection: boolean;
  showContactSection: boolean;
  businessHours: WorkingHourEntry[];
  closedDates: ClosedDate[];
  appointmentBufferMinutes: number;
  whatsAppIntegrationEnabled: boolean;
  whatsAppPhoneNumberId: string | null;
  whatsAppAccessTokenConfigured: boolean;
  whatsAppScheduledTemplate: string | null;
  whatsAppReminderTemplate: string | null;
  whatsAppCancelledTemplate: string | null;
  whatsAppRescheduledTemplate: string | null;
  whatsAppConfirmedTemplate: string | null;
  whatsAppCompletedTemplate: string | null;
  reminder24hEnabled: boolean;
  reminder2hEnabled: boolean;
  postServiceThankYouEnabled: boolean;
};

export type ClosedDate = {
  date: string;
  reason: string | null;
};

export type TenantProfileInput = {
  description?: string | null;
  phone?: string | null;
  whatsApp?: string | null;
  email?: string | null;
  address?: string | null;
  instagramUrl?: string | null;
  facebookUrl?: string | null;
};

export function getTenantProfile(accessToken: string): Promise<TenantProfile> {
  return request("/api/tenants/profile", {}, accessToken);
}

export function updateTenantProfile(input: TenantProfileInput, accessToken: string): Promise<void> {
  return request("/api/tenants/profile", { method: "PUT", body: JSON.stringify(input) }, accessToken);
}

export function setTenantBusinessHours(entries: WorkingHourEntry[], accessToken: string): Promise<void> {
  return request("/api/tenants/business-hours", { method: "PUT", body: JSON.stringify({ entries }) }, accessToken);
}

export type UpdateTenantSchedulingSettingsInput = {
  closedDates: ClosedDate[];
  appointmentBufferMinutes: number;
};

export function updateTenantSchedulingSettings(
  input: UpdateTenantSchedulingSettingsInput,
  accessToken: string
): Promise<void> {
  return request("/api/tenants/scheduling-settings", { method: "PUT", body: JSON.stringify(input) }, accessToken);
}

export type UpdateTenantWhatsAppSettingsInput = {
  enabled: boolean;
  phoneNumberId: string | null;
  // null = nao alterar o token ja salvo (a API nunca devolve o valor atual para reenviar).
  accessToken: string | null;
  scheduledTemplate: string | null;
  reminderTemplate: string | null;
  cancelledTemplate: string | null;
  rescheduledTemplate: string | null;
  confirmedTemplate: string | null;
  completedTemplate: string | null;
};

export function updateTenantWhatsAppSettings(input: UpdateTenantWhatsAppSettingsInput, accessToken: string): Promise<void> {
  return request("/api/tenants/whatsapp-settings", { method: "PUT", body: JSON.stringify(input) }, accessToken);
}

export type UpdateTenantReminderSettingsInput = {
  reminder24hEnabled: boolean;
  reminder2hEnabled: boolean;
  postServiceThankYouEnabled: boolean;
};

export function updateTenantReminderSettings(input: UpdateTenantReminderSettingsInput, accessToken: string): Promise<void> {
  return request("/api/tenants/reminder-settings", { method: "PUT", body: JSON.stringify(input) }, accessToken);
}

export type NotificationLogItem = {
  id: string;
  appointmentId: string;
  serviceName: string;
  customerId: string;
  customerName: string;
  channel: "Email" | "WhatsApp";
  trigger: "Scheduled" | "Reminder" | "Cancelled" | "Rescheduled" | "Confirmed" | "Completed";
  status: "Sent" | "Failed";
  sentAtUtc: string;
  errorMessage: string | null;
};

export function listNotificationHistory(
  params: { page?: number; pageSize?: number; customerId?: string },
  accessToken: string
): Promise<PagedResult<NotificationLogItem>> {
  const query = new URLSearchParams();
  query.set("page", String(params.page ?? 1));
  query.set("pageSize", String(params.pageSize ?? 20));
  if (params.customerId) {
    query.set("customerId", params.customerId);
  }
  return request(`/api/appointments/notifications?${query.toString()}`, {}, accessToken);
}

export type TenantPageCustomizationInput = {
  secondaryColorHex: string | null;
  font: PublicPageFont;
  buttonStyle: PublicPageButtonStyle;
  showAboutSection: boolean;
  showServicesSection: boolean;
  showTeamSection: boolean;
  showHoursSection: boolean;
  showContactSection: boolean;
};

export function updateTenantPageCustomization(input: TenantPageCustomizationInput, accessToken: string): Promise<void> {
  return request("/api/tenants/page-customization", { method: "PUT", body: JSON.stringify(input) }, accessToken);
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

export type PagedResult<T> = {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
};

function buildListQuery(params: { search?: string; page?: number; pageSize?: number }): string {
  const query = new URLSearchParams();
  if (params.search) {
    query.set("search", params.search);
  }
  query.set("page", String(params.page ?? 1));
  query.set("pageSize", String(params.pageSize ?? 20));
  return query.toString();
}

// ---------- Customers ----------

export type CustomerSegment = "Novo" | "Recorrente" | "Vip" | "EmRisco" | "Inativo" | "NoShow";

export type CustomerSummary = {
  id: string;
  fullName: string;
  email: string | null;
  phone: string | null;
  isActive: boolean;
  segment: CustomerSegment;
};

export type CustomerDetails = CustomerSummary & {
  notes: string | null;
  dateOfBirth: string | null;
  customData: Record<string, string>;
  cpf: string | null;
  healthNotes: string | null;
};

export type CustomerInput = {
  fullName: string;
  email?: string | null;
  phone?: string | null;
  notes?: string | null;
  dateOfBirth?: string | null;
  cpf?: string | null;
  healthNotes?: string | null;
};

export function listCustomers(
  params: { search?: string; page?: number; pageSize?: number; segment?: CustomerSegment },
  accessToken: string
): Promise<PagedResult<CustomerSummary>> {
  const query = new URLSearchParams(buildListQuery(params));
  if (params.segment) {
    query.set("segment", params.segment);
  }
  return request(`/api/customers?${query.toString()}`, {}, accessToken);
}

export function getCustomerById(id: string, accessToken: string): Promise<CustomerDetails> {
  return request(`/api/customers/${id}`, {}, accessToken);
}

export function createCustomer(input: CustomerInput, accessToken: string): Promise<{ id: string }> {
  return request("/api/customers", { method: "POST", body: JSON.stringify(input) }, accessToken);
}

export function updateCustomer(id: string, input: CustomerInput, accessToken: string): Promise<void> {
  return request(`/api/customers/${id}`, { method: "PUT", body: JSON.stringify(input) }, accessToken);
}

export function setCustomerActiveStatus(id: string, isActive: boolean, accessToken: string): Promise<void> {
  return request(`/api/customers/${id}/status`, { method: "PATCH", body: JSON.stringify({ isActive }) }, accessToken);
}

export function sendCustomerMessage(
  id: string, input: { subject: string; body: string }, accessToken: string
): Promise<void> {
  return request(`/api/customers/${id}/send-message`, { method: "POST", body: JSON.stringify(input) }, accessToken);
}

export type ImportCustomersResult = {
  imported: number;
  skipped: number;
  errors: string[];
};

export async function importCustomersFromCsv(file: File, accessToken: string): Promise<ImportCustomersResult> {
  const formData = new FormData();
  formData.append("file", file);

  const response = await fetch(`${API_BASE_URL}/api/customers/import`, {
    method: "POST",
    credentials: "include",
    headers: { Authorization: `Bearer ${accessToken}` },
    body: formData,
  });

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as ProblemDetails | null;
    throw new ApiError(
      problem?.detail ?? problem?.title ?? "Nao foi possivel importar o arquivo.",
      response.status,
      problem?.code
    );
  }

  return response.json();
}

// ---------- Catalog (Servicos) ----------

export type ServiceSummary = {
  id: string;
  name: string;
  durationMinutes: number;
  price: number;
  currency: string;
  category: string | null;
  displayOrder: number;
  imageUrl: string | null;
  isActive: boolean;
};

export type ServiceDetails = ServiceSummary & {
  description: string | null;
};

export type ServiceInput = {
  name: string;
  description?: string | null;
  durationMinutes: number;
  price: number;
  currency?: string;
  category?: string | null;
  displayOrder?: number;
};

export function listServices(
  params: { search?: string; page?: number; pageSize?: number },
  accessToken: string
): Promise<PagedResult<ServiceSummary>> {
  return request(`/api/services?${buildListQuery(params)}`, {}, accessToken);
}

export function getServiceById(id: string, accessToken: string): Promise<ServiceDetails> {
  return request(`/api/services/${id}`, {}, accessToken);
}

export function createService(input: ServiceInput, accessToken: string): Promise<{ id: string }> {
  return request("/api/services", { method: "POST", body: JSON.stringify(input) }, accessToken);
}

export function updateService(id: string, input: ServiceInput, accessToken: string): Promise<void> {
  return request(`/api/services/${id}`, { method: "PUT", body: JSON.stringify(input) }, accessToken);
}

export function setServiceActiveStatus(id: string, isActive: boolean, accessToken: string): Promise<void> {
  return request(`/api/services/${id}/status`, { method: "PATCH", body: JSON.stringify({ isActive }) }, accessToken);
}

export async function uploadServiceImage(id: string, file: File, accessToken: string): Promise<{ imageUrl: string }> {
  const formData = new FormData();
  formData.append("file", file);

  const response = await fetch(`${API_BASE_URL}/api/services/${id}/image`, {
    method: "POST",
    credentials: "include",
    headers: { Authorization: `Bearer ${accessToken}` },
    body: formData,
  });

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as ProblemDetails | null;
    throw new ApiError(problem?.detail ?? problem?.title ?? "Nao foi possivel enviar a imagem.", response.status, problem?.code);
  }

  return (await response.json()) as { imageUrl: string };
}

// ---------- Resources ----------

export type ResourceType = "Person" | "Room" | "Equipment";

export type DayOfWeekName =
  | "Sunday"
  | "Monday"
  | "Tuesday"
  | "Wednesday"
  | "Thursday"
  | "Friday"
  | "Saturday";

export type WorkingHourEntry = {
  dayOfWeek: DayOfWeekName;
  startTime: string;
  endTime: string;
};

export type ResourceSummary = {
  id: string;
  name: string;
  type: ResourceType;
  capacity: number;
  description: string | null;
  isActive: boolean;
  unitId: string | null;
  photoUrl: string | null;
  specialties: string[];
};

export type ResourceDetails = ResourceSummary & {
  serviceIds: string[];
  workingHours: WorkingHourEntry[];
};

export type TimeOffSummary = {
  id: string;
  startDate: string;
  endDate: string;
  reason: string | null;
};

export type ResourceInput = {
  name: string;
  type: ResourceType;
  capacity: number;
  description?: string | null;
  unitId?: string | null;
};

export function listResources(
  params: { search?: string; page?: number; pageSize?: number },
  accessToken: string
): Promise<PagedResult<ResourceSummary>> {
  return request(`/api/resources?${buildListQuery(params)}`, {}, accessToken);
}

export function getResourceById(id: string, accessToken: string): Promise<ResourceDetails> {
  return request(`/api/resources/${id}`, {}, accessToken);
}

export function createResource(input: ResourceInput, accessToken: string): Promise<{ id: string }> {
  return request("/api/resources", { method: "POST", body: JSON.stringify(input) }, accessToken);
}

export function updateResource(id: string, input: ResourceInput, accessToken: string): Promise<void> {
  return request(`/api/resources/${id}`, { method: "PUT", body: JSON.stringify(input) }, accessToken);
}

export function setResourceActiveStatus(id: string, isActive: boolean, accessToken: string): Promise<void> {
  return request(`/api/resources/${id}/status`, { method: "PATCH", body: JSON.stringify({ isActive }) }, accessToken);
}

export function setResourceWorkingHours(
  id: string,
  entries: WorkingHourEntry[],
  accessToken: string
): Promise<void> {
  return request(`/api/resources/${id}/working-hours`, { method: "PUT", body: JSON.stringify({ entries }) }, accessToken);
}

export async function uploadResourcePhoto(id: string, file: File, accessToken: string): Promise<{ photoUrl: string }> {
  const formData = new FormData();
  formData.append("file", file);

  const response = await fetch(`${API_BASE_URL}/api/resources/${id}/photo`, {
    method: "POST",
    credentials: "include",
    headers: { Authorization: `Bearer ${accessToken}` },
    body: formData,
  });

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as ProblemDetails | null;
    throw new ApiError(problem?.detail ?? problem?.title ?? "Nao foi possivel enviar a foto.", response.status, problem?.code);
  }

  return (await response.json()) as { photoUrl: string };
}

export function setResourceSpecialties(id: string, specialties: string[], accessToken: string): Promise<void> {
  return request(`/api/resources/${id}/specialties`, { method: "PUT", body: JSON.stringify({ specialties }) }, accessToken);
}

export function setResourceServices(id: string, serviceIds: string[], accessToken: string): Promise<void> {
  return request(`/api/resources/${id}/services`, { method: "PUT", body: JSON.stringify({ serviceIds }) }, accessToken);
}

export function listTimeOffs(resourceId: string, accessToken: string): Promise<TimeOffSummary[]> {
  return request(`/api/resources/${resourceId}/time-off`, {}, accessToken);
}

export function createTimeOff(
  resourceId: string,
  input: { startDate: string; endDate: string; reason?: string | null },
  accessToken: string
): Promise<{ id: string }> {
  return request(`/api/resources/${resourceId}/time-off`, { method: "POST", body: JSON.stringify(input) }, accessToken);
}

export function deleteTimeOff(timeOffId: string, accessToken: string): Promise<void> {
  return request(`/api/resources/time-off/${timeOffId}`, { method: "DELETE" }, accessToken);
}

// ---------- Units ----------

export type UnitSummary = {
  id: string;
  name: string;
  address: string | null;
  isActive: boolean;
};

export type UnitInput = {
  name: string;
  address?: string | null;
};

export function listUnits(accessToken: string): Promise<UnitSummary[]> {
  return request("/api/units", {}, accessToken);
}

export function getUnitById(id: string, accessToken: string): Promise<UnitSummary> {
  return request(`/api/units/${id}`, {}, accessToken);
}

export function createUnit(input: UnitInput, accessToken: string): Promise<{ id: string }> {
  return request("/api/units", { method: "POST", body: JSON.stringify(input) }, accessToken);
}

export function updateUnit(id: string, input: UnitInput, accessToken: string): Promise<void> {
  return request(`/api/units/${id}`, { method: "PUT", body: JSON.stringify(input) }, accessToken);
}

export function setUnitActiveStatus(id: string, isActive: boolean, accessToken: string): Promise<void> {
  return request(`/api/units/${id}/status`, { method: "PATCH", body: JSON.stringify({ isActive }) }, accessToken);
}

// ---------- Scheduling (Agenda) ----------

export type AppointmentStatus =
  | "Scheduled"
  | "Confirmed"
  | "InProgress"
  | "Completed"
  | "NoShow"
  | "CancelledByCustomer"
  | "CancelledByStaff";

export type AppointmentSummary = {
  id: string;
  customerId: string;
  resourceId: string;
  unitId: string | null;
  serviceId: string;
  serviceName: string;
  startUtc: string;
  endUtc: string;
  status: AppointmentStatus;
  price: number;
  currency: string;
  notes: string | null;
};

export type AppointmentDetails = AppointmentSummary;

export type ScheduleAppointmentInput = {
  customerId: string;
  resourceId: string;
  serviceId: string;
  startAtUtc: string;
  notes?: string | null;
};

export function listAppointments(
  params: { fromUtc: string; toUtc: string; resourceId?: string; unitId?: string },
  accessToken: string
): Promise<AppointmentSummary[]> {
  const query = new URLSearchParams({ from: params.fromUtc, to: params.toUtc });
  if (params.resourceId) {
    query.set("resourceId", params.resourceId);
  }
  if (params.unitId) {
    query.set("unitId", params.unitId);
  }
  return request(`/api/appointments?${query.toString()}`, {}, accessToken);
}

export function getAppointmentById(id: string, accessToken: string): Promise<AppointmentDetails> {
  return request(`/api/appointments/${id}`, {}, accessToken);
}

export type CustomerAppointmentHistoryItem = {
  appointmentId: string;
  serviceName: string;
  resourceId: string;
  professionalName: string;
  startUtc: string;
  endUtc: string;
  status: AppointmentStatus;
  price: number;
  currency: string;
  notes: string | null;
};

export type CustomerAppointmentHistory = {
  items: CustomerAppointmentHistoryItem[];
  totalVisits: number;
  totalSpent: number;
  totalSpentCurrency: string | null;
  lastVisitAtUtc: string | null;
  nextAppointmentAtUtc: string | null;
  favoriteServiceName: string | null;
  favoriteProfessionalName: string | null;
};

export function getCustomerAppointmentHistory(customerId: string, accessToken: string): Promise<CustomerAppointmentHistory> {
  return request(`/api/appointments/customers/${customerId}/history`, {}, accessToken);
}

export type CustomerRecoveryCandidate = {
  customerId: string;
  customerName: string;
  customerEmail: string | null;
  averageIntervalDays: number;
  daysSinceLastVisit: number;
  daysOverdue: number;
  lastVisitAtUtc: string;
};

export function getCustomerRecoveryCandidates(accessToken: string): Promise<CustomerRecoveryCandidate[]> {
  return request(`/api/appointments/customer-recovery`, {}, accessToken);
}

export function scheduleAppointment(input: ScheduleAppointmentInput, accessToken: string): Promise<{ id: string }> {
  return request("/api/appointments", { method: "POST", body: JSON.stringify(input) }, accessToken);
}

export function confirmAppointment(id: string, accessToken: string): Promise<void> {
  return request(`/api/appointments/${id}/confirm`, { method: "POST" }, accessToken);
}

export function startAppointment(id: string, accessToken: string): Promise<void> {
  return request(`/api/appointments/${id}/start`, { method: "POST" }, accessToken);
}

export function completeAppointment(id: string, accessToken: string): Promise<void> {
  return request(`/api/appointments/${id}/complete`, { method: "POST" }, accessToken);
}

export function markAppointmentNoShow(id: string, accessToken: string): Promise<void> {
  return request(`/api/appointments/${id}/no-show`, { method: "POST" }, accessToken);
}

export function cancelAppointment(id: string, byStaff: boolean, accessToken: string): Promise<void> {
  return request(`/api/appointments/${id}/cancel`, { method: "POST", body: JSON.stringify({ byStaff }) }, accessToken);
}

export function rescheduleAppointment(id: string, newStartAtUtc: string, accessToken: string): Promise<void> {
  return request(
    `/api/appointments/${id}/reschedule`,
    { method: "PUT", body: JSON.stringify({ newStartAtUtc }) },
    accessToken
  );
}

// ---------- Portal publico (sem login) ----------

export type PublicServiceSummary = {
  id: string;
  name: string;
  description: string | null;
  durationMinutes: number;
  price: number;
  currency: string;
  category: string | null;
  imageUrl: string | null;
  displayOrder: number;
};

export type PublicResourceSummary = {
  id: string;
  name: string;
  type: ResourceType;
  description: string | null;
  photoUrl: string | null;
  specialties: string[];
};

export type AvailableSlot = {
  startUtc: string;
  endUtc: string;
};

export type PublicScheduleAppointmentInput = {
  resourceId: string;
  serviceId: string;
  startAtUtc: string;
  customerFullName: string;
  customerEmail: string;
  customerPhone?: string | null;
  notes?: string | null;
};

export function publicListServices(tenantId: string): Promise<PublicServiceSummary[]> {
  return request(`/api/public/tenants/${tenantId}/services`);
}

export function publicListResources(tenantId: string): Promise<PublicResourceSummary[]> {
  return request(`/api/public/tenants/${tenantId}/resources`);
}

export function getAvailableSlots(
  tenantId: string,
  params: { resourceId: string; serviceId: string; date: string }
): Promise<AvailableSlot[]> {
  const query = new URLSearchParams(params);
  return request(`/api/public/tenants/${tenantId}/availability?${query.toString()}`);
}

export function publicScheduleAppointment(tenantId: string, input: PublicScheduleAppointmentInput): Promise<{ id: string }> {
  return request(`/api/public/tenants/${tenantId}/appointments`, { method: "POST", body: JSON.stringify(input) });
}

// ---------- Platform (Super Admin) ----------
// Autoridade separada de qualquer tenant: token proprio, nunca reaproveita
// AuthTokens/login/useSession do painel do estabelecimento.

export type PlatformAuthTokens = {
  accessToken: string;
  expiresAtUtc: string;
  fullName: string;
};

export function platformLogin(input: { email: string; password: string }): Promise<PlatformAuthTokens> {
  return request<PlatformAuthTokens>("/api/platform/auth/login", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export type TenantAdminSummary = {
  id: string;
  name: string;
  slug: string;
  timeZoneId: string;
  isActive: boolean;
};

export function listTenantsForPlatform(accessToken: string): Promise<TenantAdminSummary[]> {
  return request<TenantAdminSummary[]>("/api/platform/tenants", {}, accessToken);
}

export function setTenantActiveStatusForPlatform(
  tenantId: string,
  isActive: boolean,
  accessToken: string
): Promise<void> {
  return request(
    `/api/platform/tenants/${tenantId}/status`,
    { method: "PATCH", body: JSON.stringify({ isActive }) },
    accessToken
  );
}

// ---------- Billing (assinatura do estabelecimento) ----------

export type PlanSummary = {
  id: string;
  name: string;
  priceAmount: number;
  currency: string;
  billingCycle: string;
};

export function listPlans(accessToken: string): Promise<PlanSummary[]> {
  return request<PlanSummary[]>("/api/billing/plans", {}, accessToken);
}

export type LatestPaymentSummary = {
  status: string;
  amount: number;
  dueDate: string;
  invoiceUrl: string | null;
};

export type MySubscription = {
  planName: string;
  status: string;
  trialEndsAtUtc: string;
  currentPeriodEndsAtUtc: string | null;
  latestPayment: LatestPaymentSummary | null;
};

export function getMySubscription(accessToken: string): Promise<MySubscription> {
  return request<MySubscription>("/api/billing/subscription", {}, accessToken);
}

export function subscribeToPlan(
  input: { planId: string; fullName: string; cpfCnpj: string; email?: string },
  accessToken: string
): Promise<{ invoiceUrl: string }> {
  return request("/api/billing/subscription/subscribe", { method: "POST", body: JSON.stringify(input) }, accessToken);
}

export function cancelSubscription(accessToken: string): Promise<void> {
  return request("/api/billing/subscription/cancel", { method: "POST" }, accessToken);
}

export type SubscriptionAdminSummary = {
  tenantId: string;
  tenantName: string;
  planName: string;
  status: string;
  trialEndsAtUtc: string;
  currentPeriodEndsAtUtc: string | null;
};

export function listSubscriptionsForPlatform(accessToken: string): Promise<SubscriptionAdminSummary[]> {
  return request<SubscriptionAdminSummary[]>("/api/platform/subscriptions", {}, accessToken);
}

// ---------- Financeiro ----------

export type AccountReceivableStatus = "Pending" | "Received" | "Cancelled";
export type AccountPayableStatus = "Pending" | "Paid" | "Cancelled";
export type ExpenseCategory = "Rent" | "Supplies" | "Commission" | "Salary" | "Utilities" | "Marketing" | "Other";
export type CommissionCalculationType = "Percentage" | "FixedAmount";

export type AccountReceivableSummary = {
  id: string;
  description: string;
  amount: number;
  currency: string;
  dueDate: string;
  status: AccountReceivableStatus;
  receivedAtUtc: string | null;
  sourceAppointmentId: string | null;
};

export type AccountPayableSummary = {
  id: string;
  description: string;
  amount: number;
  currency: string;
  dueDate: string;
  category: ExpenseCategory;
  status: AccountPayableStatus;
  paidAtUtc: string | null;
  resourceId: string | null;
  sourceAppointmentId: string | null;
};

export function listAccountsReceivable(
  params: { status?: AccountReceivableStatus; from?: string; to?: string; page?: number; pageSize?: number },
  accessToken: string
): Promise<PagedResult<AccountReceivableSummary>> {
  const query = new URLSearchParams();
  if (params.status) query.set("status", params.status);
  if (params.from) query.set("from", params.from);
  if (params.to) query.set("to", params.to);
  query.set("page", String(params.page ?? 1));
  query.set("pageSize", String(params.pageSize ?? 20));
  return request(`/api/financeiro/contas-a-receber?${query.toString()}`, {}, accessToken);
}

export function markAccountReceivableReceived(id: string, accessToken: string): Promise<void> {
  return request(`/api/financeiro/contas-a-receber/${id}/receber`, { method: "PATCH" }, accessToken);
}

export function listAccountsPayable(
  params: {
    status?: AccountPayableStatus;
    category?: ExpenseCategory;
    from?: string;
    to?: string;
    page?: number;
    pageSize?: number;
  },
  accessToken: string
): Promise<PagedResult<AccountPayableSummary>> {
  const query = new URLSearchParams();
  if (params.status) query.set("status", params.status);
  if (params.category) query.set("category", params.category);
  if (params.from) query.set("from", params.from);
  if (params.to) query.set("to", params.to);
  query.set("page", String(params.page ?? 1));
  query.set("pageSize", String(params.pageSize ?? 20));
  return request(`/api/financeiro/contas-a-pagar?${query.toString()}`, {}, accessToken);
}

export type CreateAccountPayableInput = {
  description: string;
  amount: number;
  dueDate: string;
  category: ExpenseCategory;
};

export function createAccountPayable(input: CreateAccountPayableInput, accessToken: string): Promise<{ id: string }> {
  return request("/api/financeiro/contas-a-pagar", { method: "POST", body: JSON.stringify(input) }, accessToken);
}

export function markAccountPayablePaid(id: string, accessToken: string): Promise<void> {
  return request(`/api/financeiro/contas-a-pagar/${id}/pagar`, { method: "PATCH" }, accessToken);
}

export function cancelAccountPayable(id: string, accessToken: string): Promise<void> {
  return request(`/api/financeiro/contas-a-pagar/${id}/cancelar`, { method: "PATCH" }, accessToken);
}

export type CommissionRuleSummary = {
  resourceId: string;
  resourceName: string;
  calculationType: CommissionCalculationType | null;
  value: number | null;
  isActive: boolean;
};

export function listCommissionRules(accessToken: string): Promise<CommissionRuleSummary[]> {
  return request<CommissionRuleSummary[]>("/api/financeiro/comissoes", {}, accessToken);
}

export function upsertCommissionRule(
  resourceId: string,
  input: { calculationType: CommissionCalculationType; value: number },
  accessToken: string
): Promise<void> {
  return request(`/api/financeiro/comissoes/${resourceId}`, { method: "PUT", body: JSON.stringify(input) }, accessToken);
}

export function deactivateCommissionRule(resourceId: string, accessToken: string): Promise<void> {
  return request(`/api/financeiro/comissoes/${resourceId}`, { method: "DELETE" }, accessToken);
}

export type CashFlowMonthPoint = {
  month: string;
  received: number;
  paid: number;
};

export type CashFlowCategoryPoint = {
  category: string;
  total: number;
};

export type CashFlowSummary = {
  totalReceived: number;
  totalPaid: number;
  netBalance: number;
  seriesByMonth: CashFlowMonthPoint[];
  categoryBreakdown: CashFlowCategoryPoint[];
};

export function getCashFlowSummary(params: { from: string; to: string }, accessToken: string): Promise<CashFlowSummary> {
  const query = new URLSearchParams({ from: params.from, to: params.to });
  return request(`/api/financeiro/fluxo-de-caixa?${query.toString()}`, {}, accessToken);
}

// ---------- Estoque ----------

export type StockMovementType = "Entry" | "Exit";
export type StockMovementReason = "Purchase" | "Sale" | "Loss" | "Adjustment" | "Other";

export type ProductSummary = {
  id: string;
  name: string;
  sku: string | null;
  quantityInStock: number;
  minimumStock: number;
  salePrice: number | null;
  currency: string | null;
  isActive: boolean;
  isLowStock: boolean;
};

export type ProductDetails = ProductSummary & {
  description: string | null;
};

export type CreateProductInput = {
  name: string;
  sku?: string | null;
  description?: string | null;
  quantityInStock: number;
  minimumStock: number;
  salePrice?: number | null;
  currency?: string | null;
};

export type UpdateProductInput = {
  name: string;
  sku?: string | null;
  description?: string | null;
  minimumStock: number;
  salePrice?: number | null;
  currency?: string | null;
};

export function listProducts(
  params: { search?: string; isActive?: boolean; lowStockOnly?: boolean; page?: number; pageSize?: number },
  accessToken: string
): Promise<PagedResult<ProductSummary>> {
  const query = new URLSearchParams();
  if (params.search) query.set("search", params.search);
  if (params.isActive !== undefined) query.set("isActive", String(params.isActive));
  if (params.lowStockOnly) query.set("lowStockOnly", "true");
  query.set("page", String(params.page ?? 1));
  query.set("pageSize", String(params.pageSize ?? 20));
  return request(`/api/estoque/produtos?${query.toString()}`, {}, accessToken);
}

export function getProductById(id: string, accessToken: string): Promise<ProductDetails> {
  return request(`/api/estoque/produtos/${id}`, {}, accessToken);
}

export function createProduct(input: CreateProductInput, accessToken: string): Promise<{ id: string }> {
  return request("/api/estoque/produtos", { method: "POST", body: JSON.stringify(input) }, accessToken);
}

export function updateProduct(id: string, input: UpdateProductInput, accessToken: string): Promise<void> {
  return request(`/api/estoque/produtos/${id}`, { method: "PUT", body: JSON.stringify(input) }, accessToken);
}

export function setProductActiveStatus(id: string, isActive: boolean, accessToken: string): Promise<void> {
  return request(`/api/estoque/produtos/${id}/status`, { method: "PATCH", body: JSON.stringify({ isActive }) }, accessToken);
}

export type RegisterStockMovementInput = {
  type: StockMovementType;
  quantity: number;
  reason: StockMovementReason;
  notes?: string | null;
  occurredAtUtc?: string | null;
};

export function registerStockMovement(
  productId: string,
  input: RegisterStockMovementInput,
  accessToken: string
): Promise<{ id: string }> {
  return request(
    `/api/estoque/produtos/${productId}/movimentacoes`,
    { method: "POST", body: JSON.stringify(input) },
    accessToken
  );
}

export type StockMovementSummary = {
  id: string;
  productId: string;
  productName: string;
  type: StockMovementType;
  quantity: number;
  reason: StockMovementReason;
  notes: string | null;
  occurredAtUtc: string;
};

export function listStockMovements(
  params: {
    productId?: string;
    type?: StockMovementType;
    reason?: StockMovementReason;
    from?: string;
    to?: string;
    page?: number;
    pageSize?: number;
  },
  accessToken: string
): Promise<PagedResult<StockMovementSummary>> {
  const query = new URLSearchParams();
  if (params.productId) query.set("productId", params.productId);
  if (params.type) query.set("type", params.type);
  if (params.reason) query.set("reason", params.reason);
  if (params.from) query.set("from", params.from);
  if (params.to) query.set("to", params.to);
  query.set("page", String(params.page ?? 1));
  query.set("pageSize", String(params.pageSize ?? 20));
  return request(`/api/estoque/movimentacoes?${query.toString()}`, {}, accessToken);
}

// ---------- Relatorios ----------

export type ServiceRevenuePoint = { serviceName: string; total: number };
export type ProfessionalRevenuePoint = { resourceId: string; resourceName: string; total: number };

export type AppointmentStats = {
  totalCount: number;
  completedCount: number;
  noShowCount: number;
  cancelledCount: number;
  noShowRate: number;
  cancellationRate: number;
  revenueByService: ServiceRevenuePoint[];
  revenueByProfessional: ProfessionalRevenuePoint[];
};

export function getAppointmentStats(params: { from: string; to: string }, accessToken: string): Promise<AppointmentStats> {
  const query = new URLSearchParams({ from: params.from, to: params.to });
  return request(`/api/appointments/stats?${query.toString()}`, {}, accessToken);
}

export type StockValueByCurrency = { currency: string; total: number };

export type InventorySummary = {
  activeProductCount: number;
  lowStockCount: number;
  totalStockValue: StockValueByCurrency[];
};

export function getInventorySummary(accessToken: string): Promise<InventorySummary> {
  return request("/api/estoque/resumo", {}, accessToken);
}

// ---------- Marketing ----------

export type CampaignSummary = {
  id: string;
  subject: string;
  recipientCount: number;
  sentAtUtc: string;
};

export type SendCampaignInput = {
  subject: string;
  body: string;
};

export function sendCampaign(input: SendCampaignInput, accessToken: string): Promise<{ id: string; recipientCount: number }> {
  return request("/api/marketing/campanhas", { method: "POST", body: JSON.stringify(input) }, accessToken);
}

export function listCampaigns(
  params: { page?: number; pageSize?: number },
  accessToken: string
): Promise<PagedResult<CampaignSummary>> {
  const query = new URLSearchParams();
  query.set("page", String(params.page ?? 1));
  query.set("pageSize", String(params.pageSize ?? 20));
  return request(`/api/marketing/campanhas?${query.toString()}`, {}, accessToken);
}
