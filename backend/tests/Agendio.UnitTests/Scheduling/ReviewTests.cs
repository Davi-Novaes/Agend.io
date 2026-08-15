using Agendio.Modules.Scheduling.Domain;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.UnitTests.Scheduling;

public class ReviewTests
{
    private static readonly TenantId Tenant = TenantId.From(Guid.NewGuid());
    private static readonly AppointmentId Appointment = AppointmentId.New();
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly Guid Resource = Guid.NewGuid();

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Create_Should_Fail_When_Rating_Is_Outside_1_To_5(int rating)
    {
        var result = Review.Create(Tenant, Appointment, Customer, Resource, "Corte", rating, null);

        result.IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Create_Should_Succeed_With_Valid_Rating(int rating)
    {
        var result = Review.Create(Tenant, Appointment, Customer, Resource, "Corte", rating, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Rating.ShouldBe(rating);
    }

    [Fact]
    public void Create_Should_Trim_Comment()
    {
        var result = Review.Create(Tenant, Appointment, Customer, Resource, "Corte", 5, "  Otimo atendimento!  ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Comment.ShouldBe("Otimo atendimento!");
    }

    [Fact]
    public void Create_Should_Store_Null_Comment_When_Blank()
    {
        var result = Review.Create(Tenant, Appointment, Customer, Resource, "Corte", 5, "   ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Comment.ShouldBeNull();
    }

    [Fact]
    public void Create_Should_Fail_When_Comment_Exceeds_Max_Length()
    {
        var longComment = new string('a', 1001);

        var result = Review.Create(Tenant, Appointment, Customer, Resource, "Corte", 5, longComment);

        result.IsFailure.ShouldBeTrue();
    }
}
