using Agendio.Modules.Tenancy.Domain;

namespace Agendio.UnitTests.Tenancy;

public class ContrastValidatorTests
{
    [Theory]
    [InlineData("#4F46E5")] // indigo-600, usado como --primary padrao do produto (~8.3:1)
    [InlineData("#000000")]
    [InlineData("#1F2937")]
    public void MeetsAaContrast_Should_Accept_Colors_With_Enough_Contrast_Against_White(string hex)
    {
        ContrastValidator.MeetsAaContrast("#FFFFFF", hex).ShouldBeTrue();
    }

    [Theory]
    [InlineData("#FFFF00")] // amarelo: claro demais, contraste baixo com texto branco
    [InlineData("#F5F5F5")]
    [InlineData("#FFFFFF")]
    public void MeetsAaContrast_Should_Reject_Colors_Too_Light_Against_White(string hex)
    {
        ContrastValidator.MeetsAaContrast("#FFFFFF", hex).ShouldBeFalse();
    }

    [Theory]
    [InlineData("#4F46E5")]
    [InlineData("#000000")]
    [InlineData("#abcdef")]
    public void IsValidHexColor_Should_Accept_Well_Formed_Hex(string hex)
    {
        ContrastValidator.IsValidHexColor(hex).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("4F46E5")]
    [InlineData("#FFF")]
    [InlineData("#ZZZZZZ")]
    public void IsValidHexColor_Should_Reject_Malformed_Input(string? hex)
    {
        ContrastValidator.IsValidHexColor(hex).ShouldBeFalse();
    }

    [Fact]
    public void ContrastRatio_Between_Black_And_White_Should_Be_Maximum()
    {
        ContrastValidator.ContrastRatio("#000000", "#FFFFFF").ShouldBe(21, tolerance: 0.01);
    }
}
