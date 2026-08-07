namespace Agendio.Modules.Scheduling.Domain;

public enum AppointmentStatus
{
    Scheduled,
    Confirmed,
    InProgress,
    Completed,
    NoShow,
    CancelledByCustomer,
    CancelledByStaff,
}
