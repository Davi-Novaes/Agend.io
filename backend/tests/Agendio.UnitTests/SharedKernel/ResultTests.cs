using Agendio.SharedKernel.Results;

namespace Agendio.UnitTests.SharedKernel;

public class ResultTests
{
    [Fact]
    public void Success_Should_Have_No_Error()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Error.ShouldBe(Error.None);
    }

    [Fact]
    public void Failure_Should_Expose_The_Given_Error()
    {
        var error = Error.Validation("Test.Invalid", "invalido");

        var result = Result.Failure(error);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }

    [Fact]
    public void Creating_A_Success_Result_With_An_Error_Should_Throw()
    {
        Should.Throw<InvalidOperationException>(() => new TestableResult(true, Error.Validation("x", "y")));
    }

    [Fact]
    public void Creating_A_Failure_Result_Without_An_Error_Should_Throw()
    {
        Should.Throw<InvalidOperationException>(() => new TestableResult(false, Error.None));
    }

    [Fact]
    public void Generic_Success_Should_Expose_Value()
    {
        var result = Result.Success(42);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void Accessing_Value_Of_A_Failure_Should_Throw()
    {
        var result = Result.Failure<int>(Error.NotFound("Test.NotFound", "nao encontrado"));

        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Implicit_Conversion_From_Value_Should_Produce_Success()
    {
        Result<string> result = "hello";

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("hello");
    }

    // Expoe o construtor protegido apenas para testar a validacao de invariante.
    private sealed class TestableResult(bool isSuccess, Error error) : Result(isSuccess, error);
}
