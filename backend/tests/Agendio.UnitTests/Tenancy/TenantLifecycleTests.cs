using Agendio.Modules.Tenancy.Domain;

namespace Agendio.UnitTests.Tenancy;

public class TenantLifecycleTests
{
    private static Tenant CreateTenant() =>
        Tenant.Create("Barbearia Modelo", "barbearia-modelo", BusinessType.Barbershop, "America/Sao_Paulo").Value;

    [Fact]
    public void MarkFirstUserRegistered_Should_Set_Timestamp_Only_On_First_Call()
    {
        var tenant = CreateTenant();
        var firstCall = DateTimeOffset.UtcNow;

        tenant.MarkFirstUserRegistered(firstCall);
        tenant.MarkFirstUserRegistered(firstCall.AddMinutes(5));

        tenant.FirstUserRegisteredAtUtc.ShouldBe(firstCall);
    }

    [Fact]
    public void ReleaseAbandonedSlug_Should_Fail_When_Tenant_Already_Has_A_Registered_User()
    {
        var tenant = CreateTenant();
        tenant.MarkFirstUserRegistered(DateTimeOffset.UtcNow);

        var result = tenant.ReleaseAbandonedSlug();

        result.IsFailure.ShouldBeTrue();
        tenant.Slug.Value.ShouldBe("barbearia-modelo");
        tenant.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void ReleaseAbandonedSlug_Should_Rename_Slug_And_Deactivate_When_Never_Registered()
    {
        var tenant = CreateTenant();

        var result = tenant.ReleaseAbandonedSlug();

        result.IsSuccess.ShouldBeTrue();
        tenant.Slug.Value.ShouldNotBe("barbearia-modelo");
        tenant.Slug.Value.ShouldContain(tenant.Id.Value.ToString("N"));
        tenant.IsActive.ShouldBeFalse();
    }
}
