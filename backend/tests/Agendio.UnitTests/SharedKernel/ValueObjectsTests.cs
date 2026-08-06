using Agendio.SharedKernel.ValueObjects;

namespace Agendio.UnitTests.SharedKernel;

public class EmailTests
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("  USER@EXAMPLE.COM  ")]
    [InlineData("first.last+tag@sub.domain.com")]
    public void Create_Should_Succeed_For_Valid_Addresses(string input)
    {
        var result = Email.Create(input);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(input.Trim().ToLowerInvariant());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    [InlineData("@missing-local.com")]
    public void Create_Should_Fail_For_Invalid_Addresses(string? input)
    {
        var result = Email.Create(input);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Two_Emails_With_Different_Casing_Should_Be_Equal()
    {
        var a = Email.Create("User@Example.com").Value;
        var b = Email.Create("user@example.com").Value;

        a.ShouldBe(b);
    }
}

public class PhoneNumberTests
{
    [Theory]
    [InlineData("11999998888", "+5511999998888")]
    [InlineData("(11) 99999-8888", "+5511999998888")]
    [InlineData("+55 11 99999-8888", "+5511999998888")]
    public void Create_Should_Normalize_To_E164(string input, string expected)
    {
        var result = PhoneNumber.Create(input);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    public void Create_Should_Fail_For_Invalid_Numbers(string? input)
    {
        var result = PhoneNumber.Create(input);

        result.IsFailure.ShouldBeTrue();
    }
}

public class SlugTests
{
    [Theory]
    [InlineData("Barbearia do Ze", "barbearia-do-ze")]
    [InlineData("Salão São José", "salao-sao-jose")]
    [InlineData("  Clínica--Bem  Estar  ", "clinica-bem-estar")]
    [InlineData("Pet Shop 123", "pet-shop-123")]
    public void Create_Should_Normalize_Correctly(string input, string expected)
    {
        var result = Slug.Create(input);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void Create_Should_Fail_When_Nothing_Alphanumeric_Remains(string? input)
    {
        var result = Slug.Create(input);

        result.IsFailure.ShouldBeTrue();
    }
}

public class MoneyTests
{
    [Fact]
    public void Create_Should_Round_To_Two_Decimal_Places()
    {
        var result = Money.Create(10.005m);

        result.Value.Amount.ShouldBe(10.00m);
    }

    [Fact]
    public void Create_Should_Fail_For_Negative_Amount()
    {
        var result = Money.Create(-1m);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Adding_Two_Money_In_Same_Currency_Should_Sum_Amounts()
    {
        var a = Money.Create(10m).Value;
        var b = Money.Create(5m).Value;

        (a + b).Amount.ShouldBe(15m);
    }

    [Fact]
    public void Adding_Different_Currencies_Should_Throw()
    {
        var brl = Money.Create(10m, "BRL").Value;
        var usd = Money.Create(10m, "USD").Value;

        Should.Throw<InvalidOperationException>(() => brl.Add(usd));
    }
}

public class TimeSlotTests
{
    [Fact]
    public void Create_Should_Fail_When_End_Is_Before_Start()
    {
        var now = DateTimeOffset.UtcNow;

        var result = TimeSlot.Create(now, now.AddMinutes(-1));

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Overlaps_Should_Detect_Intersecting_Slots()
    {
        var start = DateTimeOffset.UtcNow;
        var a = TimeSlot.Create(start, start.AddHours(1)).Value;
        var b = TimeSlot.Create(start.AddMinutes(30), start.AddHours(2)).Value;

        a.Overlaps(b).ShouldBeTrue();
    }

    [Fact]
    public void Overlaps_Should_Be_False_For_Adjacent_Slots()
    {
        var start = DateTimeOffset.UtcNow;
        var a = TimeSlot.Create(start, start.AddHours(1)).Value;
        var b = TimeSlot.Create(start.AddHours(1), start.AddHours(2)).Value;

        a.Overlaps(b).ShouldBeFalse();
    }
}
