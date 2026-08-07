namespace Agendio.Modules.Resources.Domain;

/// <summary>
/// O que a agenda de fato reserva — nunca so "funcionario": uma sala de exame ou
/// um box de lavagem tambem sao recursos, e um agendamento pode exigir mais de um
/// (pessoa + sala) ao mesmo tempo (motor de disponibilidade, Sprint 3).
/// </summary>
public enum ResourceType
{
    Person,
    Room,
    Equipment,
}
