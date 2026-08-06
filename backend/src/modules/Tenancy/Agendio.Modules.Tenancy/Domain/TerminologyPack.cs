namespace Agendio.Modules.Tenancy.Domain;

/// <summary>
/// Vocabulario da UI para um segmento: "Cliente" vira "Paciente" numa clinica e
/// "Tutor" num pet shop. So texto — nunca muda regra de negocio nem schema.
/// </summary>
public sealed record TerminologyPack(
    string Customer,
    string CustomerPlural,
    string Service,
    string ServicePlural,
    string Staff,
    string StaffPlural,
    string Appointment
);
