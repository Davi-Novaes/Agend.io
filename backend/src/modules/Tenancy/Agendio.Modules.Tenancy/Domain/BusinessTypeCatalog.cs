namespace Agendio.Modules.Tenancy.Domain;

public sealed record BusinessTypeDefinition(BusinessType BusinessType, string DisplayName, TerminologyPack Terminology);

/// <summary>
/// Catalogo estatico: cada segmento ja chega com nome exibido e vocabulario
/// prontos, sem o dono do negocio configurar nada no onboarding. Sugestao de
/// servicos por segmento fica para quando o modulo Catalog existir (Sprint 2).
/// </summary>
public static class BusinessTypeCatalog
{
    private static readonly TerminologyPack Default = new(
        Customer: "Cliente", CustomerPlural: "Clientes",
        Service: "Servico", ServicePlural: "Servicos",
        Staff: "Profissional", StaffPlural: "Profissionais",
        Appointment: "Agendamento");

    private static readonly IReadOnlyDictionary<BusinessType, BusinessTypeDefinition> Definitions =
        new Dictionary<BusinessType, BusinessTypeDefinition>
        {
            [BusinessType.Barbershop] = new(BusinessType.Barbershop, "Barbearia", Default with
            {
                Staff = "Barbeiro", StaffPlural = "Barbeiros",
            }),
            [BusinessType.HairSalon] = new(BusinessType.HairSalon, "Salao de Beleza", Default),
            [BusinessType.DentalClinic] = new(BusinessType.DentalClinic, "Clinica Odontologica", new TerminologyPack(
                Customer: "Paciente", CustomerPlural: "Pacientes",
                Service: "Procedimento", ServicePlural: "Procedimentos",
                Staff: "Dentista", StaffPlural: "Dentistas",
                Appointment: "Consulta")),
            [BusinessType.MedicalClinic] = new(BusinessType.MedicalClinic, "Clinica Medica", new TerminologyPack(
                Customer: "Paciente", CustomerPlural: "Pacientes",
                Service: "Consulta", ServicePlural: "Consultas",
                Staff: "Medico", StaffPlural: "Medicos",
                Appointment: "Consulta")),
            [BusinessType.AestheticClinic] = new(BusinessType.AestheticClinic, "Clinica de Estetica", new TerminologyPack(
                Customer: "Paciente", CustomerPlural: "Pacientes",
                Service: "Procedimento", ServicePlural: "Procedimentos",
                Staff: "Especialista", StaffPlural: "Especialistas",
                Appointment: "Consulta")),
            [BusinessType.Psychologist] = new(BusinessType.Psychologist, "Psicologia", new TerminologyPack(
                Customer: "Paciente", CustomerPlural: "Pacientes",
                Service: "Sessao", ServicePlural: "Sessoes",
                Staff: "Psicologo", StaffPlural: "Psicologos",
                Appointment: "Sessao")),
            [BusinessType.Physiotherapist] = new(BusinessType.Physiotherapist, "Fisioterapia", new TerminologyPack(
                Customer: "Paciente", CustomerPlural: "Pacientes",
                Service: "Sessao", ServicePlural: "Sessoes",
                Staff: "Fisioterapeuta", StaffPlural: "Fisioterapeutas",
                Appointment: "Sessao")),
            [BusinessType.Nutritionist] = new(BusinessType.Nutritionist, "Nutricao", new TerminologyPack(
                Customer: "Paciente", CustomerPlural: "Pacientes",
                Service: "Consulta", ServicePlural: "Consultas",
                Staff: "Nutricionista", StaffPlural: "Nutricionistas",
                Appointment: "Consulta")),
            [BusinessType.PersonalTrainer] = new(BusinessType.PersonalTrainer, "Personal Trainer", new TerminologyPack(
                Customer: "Aluno", CustomerPlural: "Alunos",
                Service: "Treino", ServicePlural: "Treinos",
                Staff: "Personal Trainer", StaffPlural: "Personal Trainers",
                Appointment: "Treino")),
            [BusinessType.Gym] = new(BusinessType.Gym, "Academia", new TerminologyPack(
                Customer: "Aluno", CustomerPlural: "Alunos",
                Service: "Aula", ServicePlural: "Aulas",
                Staff: "Instrutor", StaffPlural: "Instrutores",
                Appointment: "Aula")),
            [BusinessType.PetShop] = new(BusinessType.PetShop, "Pet Shop", Default with
            {
                Customer = "Tutor", CustomerPlural = "Tutores",
            }),
            [BusinessType.CarWash] = new(BusinessType.CarWash, "Lava-Rapido", Default with
            {
                Service = "Lavagem", ServicePlural = "Lavagens",
                Staff = "Lavador", StaffPlural = "Lavadores",
            }),
            [BusinessType.AutoRepairShop] = new(BusinessType.AutoRepairShop, "Oficina Mecanica", Default with
            {
                Staff = "Mecanico", StaffPlural = "Mecanicos",
            }),
            [BusinessType.Consulting] = new(BusinessType.Consulting, "Consultoria", Default with
            {
                Service = "Consultoria", ServicePlural = "Consultorias",
                Staff = "Consultor", StaffPlural = "Consultores",
                Appointment = "Reuniao",
            }),
            [BusinessType.LawFirm] = new(BusinessType.LawFirm, "Advocacia", Default with
            {
                Staff = "Advogado", StaffPlural = "Advogados",
                Appointment = "Consulta",
            }),
            [BusinessType.Accounting] = new(BusinessType.Accounting, "Contabilidade", Default with
            {
                Staff = "Contador", StaffPlural = "Contadores",
                Appointment = "Reuniao",
            }),
            [BusinessType.Studio] = new(BusinessType.Studio, "Estudio", Default with
            {
                Service = "Sessao", ServicePlural = "Sessoes",
                Appointment = "Sessao",
            }),
            [BusinessType.Photographer] = new(BusinessType.Photographer, "Fotografia", Default with
            {
                Service = "Sessao", ServicePlural = "Sessoes",
                Staff = "Fotografo", StaffPlural = "Fotografos",
                Appointment = "Sessao",
            }),
            [BusinessType.Other] = new(BusinessType.Other, "Outro", Default),
        };

    public static IReadOnlyCollection<BusinessTypeDefinition> All => [.. Definitions.Values];

    public static BusinessTypeDefinition Get(BusinessType businessType) => Definitions[businessType];
}
