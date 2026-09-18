using Agendio.Modules.Platform.Domain;

namespace Agendio.UnitTests.Platform;

public class PlatformAdminTests
{
    private static PlatformAdmin CreateAdmin() =>
        PlatformAdmin.Create("admin@example.com", "Admin", "hash-de-senha").Value;

    [Fact]
    public void EnableMfa_Should_Set_Secret_And_Turn_Flag_On()
    {
        var admin = CreateAdmin();

        admin.EnableMfa("SECRETOBASE32");

        admin.MfaEnabled.ShouldBeTrue();
        admin.MfaSecretEncrypted.ShouldBe("SECRETOBASE32");
    }

    [Fact]
    public void DisableMfa_Should_Clear_Secret_And_Turn_Flag_Off()
    {
        var admin = CreateAdmin();
        admin.EnableMfa("SECRETOBASE32");

        admin.DisableMfa();

        admin.MfaEnabled.ShouldBeFalse();
        admin.MfaSecretEncrypted.ShouldBeNull();
    }

    [Fact]
    public void RegisterFailedLoginAttempt_Should_Lock_Account_On_Fifth_Attempt()
    {
        var admin = CreateAdmin();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            admin.RegisterFailedLoginAttempt(now);
        }

        admin.IsLockedOut(now).ShouldBeTrue();
        admin.IsLockedOut(now.AddMinutes(15).AddSeconds(1)).ShouldBeFalse();
    }

    [Fact]
    public void RegisterSuccessfulLogin_Should_Reset_Counter_And_Clear_Lock()
    {
        var admin = CreateAdmin();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            admin.RegisterFailedLoginAttempt(now);
        }

        admin.RegisterSuccessfulLogin();

        admin.FailedLoginAttemptCount.ShouldBe(0);
        admin.IsLockedOut(now).ShouldBeFalse();
    }
}
