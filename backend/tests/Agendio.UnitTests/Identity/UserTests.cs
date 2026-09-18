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

    [Fact]
    public void RegisterFailedLoginAttempt_Should_Not_Lock_Before_Fifth_Attempt()
    {
        var user = CreateUser();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 4; i++)
        {
            user.RegisterFailedLoginAttempt(now);
        }

        user.FailedLoginAttemptCount.ShouldBe(4);
        user.IsLockedOut(now).ShouldBeFalse();
    }

    [Fact]
    public void RegisterFailedLoginAttempt_Should_Lock_Account_On_Fifth_Attempt()
    {
        var user = CreateUser();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            user.RegisterFailedLoginAttempt(now);
        }

        // Primeiro bloqueio (LockoutEscalationLevel 1) dura 1 minuto -- ver
        // LockoutDurationsByEscalationLevel em User.cs.
        user.IsLockedOut(now).ShouldBeTrue();
        user.IsLockedOut(now.AddMinutes(1).AddSeconds(-1)).ShouldBeTrue();
        user.IsLockedOut(now.AddMinutes(1).AddSeconds(1)).ShouldBeFalse();
    }

    [Fact]
    public void RegisterFailedLoginAttempt_Should_Escalate_Lockout_Duration_On_Second_Consecutive_Lock()
    {
        var user = CreateUser();
        var now = DateTimeOffset.UtcNow;

        // Primeiro ciclo: 5 tentativas trancam por 1 minuto (nivel 1).
        for (var i = 0; i < 5; i++)
        {
            user.RegisterFailedLoginAttempt(now);
        }

        // Segundo ciclo comeca so depois do primeiro bloqueio expirar --
        // LockoutEscalationLevel nao reseta entre ciclos, entao este trava
        // por 15 minutos (nivel 2), nao 1 minuto de novo.
        var secondCycleStart = now.AddMinutes(1).AddSeconds(1);
        for (var i = 0; i < 5; i++)
        {
            user.RegisterFailedLoginAttempt(secondCycleStart);
        }

        user.IsLockedOut(secondCycleStart.AddMinutes(15).AddSeconds(-1)).ShouldBeTrue();
        user.IsLockedOut(secondCycleStart.AddMinutes(15).AddSeconds(1)).ShouldBeFalse();
    }

    [Fact]
    public void RegisterFailedLoginAttempt_After_Lock_Expired_Should_Restart_Count_From_One()
    {
        var user = CreateUser();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            user.RegisterFailedLoginAttempt(now);
        }

        var afterLockExpired = now.AddMinutes(16);
        user.RegisterFailedLoginAttempt(afterLockExpired);

        user.FailedLoginAttemptCount.ShouldBe(1);
        user.IsLockedOut(afterLockExpired).ShouldBeFalse();
    }

    [Fact]
    public void RegisterSuccessfulLogin_Should_Reset_Counter_And_Clear_Lock()
    {
        var user = CreateUser();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            user.RegisterFailedLoginAttempt(now);
        }

        user.RegisterSuccessfulLogin();

        user.FailedLoginAttemptCount.ShouldBe(0);
        user.IsLockedOut(now).ShouldBeFalse();
    }

    [Fact]
    public void ResetPassword_Should_Replace_Hash_And_Clear_Pending_Token_And_Lockout()
    {
        var user = CreateUser();
        var now = DateTimeOffset.UtcNow;
        user.GeneratePasswordResetToken("token-hash", now.AddMinutes(30));
        user.RegisterFailedLoginAttempt(now);

        var result = user.ResetPassword("novo-hash-de-senha", now);

        result.IsSuccess.ShouldBeTrue();
        user.PasswordHash.ShouldBe("novo-hash-de-senha");
        user.PasswordResetTokenHash.ShouldBeNull();
        user.PasswordResetTokenExpiresAtUtc.ShouldBeNull();
        user.FailedLoginAttemptCount.ShouldBe(0);
    }

    [Fact]
    public void ConsumePasswordResetToken_Should_Fail_When_No_Token_Was_Generated()
    {
        var user = CreateUser();

        var result = user.ConsumePasswordResetToken(DateTimeOffset.UtcNow);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void ConsumePasswordResetToken_Should_Fail_When_Token_Expired()
    {
        var user = CreateUser();
        var now = DateTimeOffset.UtcNow;
        user.GeneratePasswordResetToken("token-hash", now.AddMinutes(30));

        var result = user.ConsumePasswordResetToken(now.AddMinutes(31));

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void ConsumePasswordResetToken_Should_Succeed_When_Token_Still_Valid()
    {
        var user = CreateUser();
        var now = DateTimeOffset.UtcNow;
        user.GeneratePasswordResetToken("token-hash", now.AddMinutes(30));

        var result = user.ConsumePasswordResetToken(now.AddMinutes(10));

        result.IsSuccess.ShouldBeTrue();
    }
}
