namespace Agendio.Infrastructure.Security;

/// <summary>
/// Abstracao propria (nao a do ASP.NET Core Identity): evitamos arrastar o
/// modelo de usuario/EF stores do Identity, que assume um unico store global e
/// nao se encaixa bem no nosso User multi-tenant com RLS. So o hashing
/// idiomatico foi reaproveitado — via Argon2PasswordHasher.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
