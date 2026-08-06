namespace Agendio.Modules.Identity.Domain;

/// <summary>
/// Papel simples para o Sprint 0 (Owner cria a conta ao criar o tenant, Staff e
/// convidado depois). RBAC granular por permissao (appointments:write etc.) e
/// trabalho do Sprint 1 — nao antecipamos aqui para nao overengineering.
/// </summary>
public enum UserRole
{
    Owner = 0,
    Staff = 1,
}
