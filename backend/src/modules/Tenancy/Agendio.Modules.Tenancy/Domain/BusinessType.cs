namespace Agendio.Modules.Tenancy.Domain;

/// <summary>
/// Segmento do estabelecimento. Dirige o vocabulario (<see cref="TerminologyPack"/>)
/// e, futuramente, o catalogo de servicos sugerido no onboarding — sem isso a
/// plataforma seria generica demais para "nao ser dificil de configurar".
/// </summary>
public enum BusinessType
{
    Barbershop,
    HairSalon,
    DentalClinic,
    MedicalClinic,
    AestheticClinic,
    Psychologist,
    Physiotherapist,
    Nutritionist,
    PersonalTrainer,
    Gym,
    PetShop,
    CarWash,
    AutoRepairShop,
    Consulting,
    LawFirm,
    Accounting,
    Studio,
    Photographer,
    Other,
}
