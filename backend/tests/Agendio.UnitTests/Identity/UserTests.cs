using Agendio.Modules.Identity.Domain;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.UnitTests.Identity;

public class UserTests
{
    private static User CreateUser() =>
        User.Register(TenantId.From(Guid.NewGuid()), Email.Create("dono@example.com").Value, "Dono", "hash-de-senha").Value;

    [Fact]
    public void EnableMfa_Should_Set_Secret_And_Turn_Flag_On()
    {
        var user = CreateUser();

        user.EnableMfa("SECRETOBASE32");

        user.MfaEnabled.ShouldBeTrue();
        user.MfaSecretEncrypted.ShouldBe("SECRETOBASE32");
    }

    [Fact]
    public void DisableMfa_Should_Clear_Secret_And_Turn_Flag_Off()
    {
        var user = CreateUser();
        user.EnableMfa("SECRETOBASE32");

        user.DisableMfa();

        user.MfaEnabled.ShouldBeFalse();
        user.MfaSecretEncrypted.ShouldBeNull();
    }
}
