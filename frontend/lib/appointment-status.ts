import type { AppointmentStatus } from "@/lib/api/client";

export const APPOINTMENT_STATUS_LABELS: Record<AppointmentStatus, string> = {
  Scheduled: "Agendado",
  Confirmed: "Confirmado",
  InProgress: "Em andamento",
  Completed: "Concluido",
  NoShow: "Nao compareceu",
  CancelledByCustomer: "Cancelado (cliente)",
  CancelledByStaff: "Cancelado (equipe)",
};

export const APPOINTMENT_STATUS_VARIANTS: Record<AppointmentStatus, "secondary" | "info" | "default" | "success" | "destructive"> = {
  Scheduled: "secondary",
  Confirmed: "info",
  InProgress: "default",
  Completed: "success",
  NoShow: "destructive",
  CancelledByCustomer: "destructive",
  CancelledByStaff: "destructive",
};
