using Agendio.Modules.Tenancy.Domain;

namespace Agendio.UnitTests.Tenancy;

public class TenantProfileTests
{
    private static Tenant CreateTenant() =>
        Tenant.Create("Barbearia Modelo", "barbearia-modelo", BusinessType.Barbershop, "America/Sao_Paulo").Value;

    [Fact]
    public void UpdateProfile_Should_Succeed_With_All_Fields_Empty()
    {
        var tenant = CreateTenant();

        var result = tenant.UpdateProfile(null, null, null, null, null, null, null);

        result.IsSuccess.ShouldBeTrue();
        tenant.Description.ShouldBeNull();
        tenant.Phone.ShouldBeNull();
        tenant.WhatsApp.ShouldBeNull();
        tenant.Email.ShouldBeNull();
        tenant.Address.ShouldBeNull();
        tenant.InstagramUrl.ShouldBeNull();
        tenant.FacebookUrl.ShouldBeNull();
    }

    [Fact]
    public void UpdateProfile_Should_Set_All_Fields_With_Valid_Data()
    {
        var tenant = CreateTenant();

        var result = tenant.UpdateProfile(
            "  Cortes classicos e modernos  ", "11999998888", "11988887777", "contato@barbearia.com", "Rua das Flores, 100",
            "https://instagram.com/barbearia", "https://facebook.com/barbearia");

        result.IsSuccess.ShouldBeTrue();
        tenant.Description.ShouldBe("Cortes classicos e modernos");
        tenant.Phone!.Value.ShouldBe("+5511999998888");
        tenant.WhatsApp!.Value.ShouldBe("+5511988887777");
        tenant.Email!.Value.ShouldBe("contato@barbearia.com");
        tenant.Address.ShouldBe("Rua das Flores, 100");
        tenant.InstagramUrl.ShouldBe("https://instagram.com/barbearia");
        tenant.FacebookUrl.ShouldBe("https://facebook.com/barbearia");
    }

    [Fact]
    public void UpdateProfile_Should_Fail_With_Invalid_Email()
    {
        var tenant = CreateTenant();

        var result = tenant.UpdateProfile(null, null, null, "nao-e-um-email", null, null, null);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void UpdateProfile_Should_Fail_With_Invalid_Phone()
    {
        var tenant = CreateTenant();

        var result = tenant.UpdateProfile(null, "123", null, null, null, null, null);

        result.IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData("nao-e-uma-url")]
    [InlineData("ftp://instagram.com/x")]
    public void UpdateProfile_Should_Fail_With_Invalid_Social_Url(string invalidUrl)
    {
        var tenant = CreateTenant();

        var result = tenant.UpdateProfile(null, null, null, null, null, invalidUrl, null);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void SetBusinessHours_Should_Replace_The_Whole_Week()
    {
        var tenant = CreateTenant();
        tenant.SetBusinessHours(
        [
            (DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0)),
            (DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(18, 0)),
        ]);

        var result = tenant.SetBusinessHours([(DayOfWeek.Saturday, new TimeOnly(10, 0), new TimeOnly(14, 0))]);

        result.IsSuccess.ShouldBeTrue();
        tenant.BusinessHours.Count.ShouldBe(1);
        tenant.BusinessHours.Single().DayOfWeek.ShouldBe(DayOfWeek.Saturday);
    }

    [Fact]
    public void SetBusinessHours_Should_Fail_When_End_Time_Is_Not_After_Start_Time()
    {
        var tenant = CreateTenant();

        var result = tenant.SetBusinessHours([(DayOfWeek.Monday, new TimeOnly(18, 0), new TimeOnly(9, 0))]);

        result.IsFailure.ShouldBeTrue();
        tenant.BusinessHours.ShouldBeEmpty();
    }

    [Fact]
    public void SetBanner_Should_Set_The_Url()
    {
        var tenant = CreateTenant();

        var result = tenant.SetBanner("/uploads/tenant-banners/abc.png");

        result.IsSuccess.ShouldBeTrue();
        tenant.BannerUrl.ShouldBe("/uploads/tenant-banners/abc.png");
    }

    [Fact]
    public void SetBanner_Should_Fail_With_Empty_Url()
    {
        var tenant = CreateTenant();

        var result = tenant.SetBanner("   ");

        result.IsFailure.ShouldBeTrue();
        tenant.BannerUrl.ShouldBeNull();
    }

    [Fact]
    public void UpdatePageCustomization_Should_Set_All_Fields_With_Valid_Data()
    {
        var tenant = CreateTenant();

        var result = tenant.UpdatePageCustomization(
            "#0F172A", PublicPageFont.Poppins, PublicPageButtonStyle.Pill, false, true, false, true, false);

        result.IsSuccess.ShouldBeTrue();
        tenant.SecondaryColorHex.ShouldBe("#0F172A");
        tenant.Font.ShouldBe(PublicPageFont.Poppins);
        tenant.ButtonStyle.ShouldBe(PublicPageButtonStyle.Pill);
        tenant.ShowAboutSection.ShouldBeFalse();
        tenant.ShowServicesSection.ShouldBeTrue();
        tenant.ShowTeamSection.ShouldBeFalse();
        tenant.ShowHoursSection.ShouldBeTrue();
        tenant.ShowContactSection.ShouldBeFalse();
    }

    [Fact]
    public void UpdatePageCustomization_Should_Accept_A_Null_Secondary_Color()
    {
        var tenant = CreateTenant();

        var result = tenant.UpdatePageCustomization(
            null, PublicPageFont.Default, PublicPageButtonStyle.Rounded, true, true, true, true, true);

        result.IsSuccess.ShouldBeTrue();
        tenant.SecondaryColorHex.ShouldBeNull();
    }

    [Fact]
    public void UpdatePageCustomization_Should_Reject_A_Secondary_Color_Without_Enough_Contrast()
    {
        var tenant = CreateTenant();

        // Amarelo claro: falha o minimo AA de 4.5:1 contra texto branco.
        var result = tenant.UpdatePageCustomization(
            "#FFFF00", PublicPageFont.Default, PublicPageButtonStyle.Rounded, true, true, true, true, true);

        result.IsFailure.ShouldBeTrue();
        tenant.SecondaryColorHex.ShouldBeNull();
    }

    [Fact]
    public void UpdatePageCustomization_Should_Reject_An_Invalid_Hex_Format()
    {
        var tenant = CreateTenant();

        var result = tenant.UpdatePageCustomization(
            "azul", PublicPageFont.Default, PublicPageButtonStyle.Rounded, true, true, true, true, true);

        result.IsFailure.ShouldBeTrue();
    }
}
