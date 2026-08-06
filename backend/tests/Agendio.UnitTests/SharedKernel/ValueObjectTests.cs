using Agendio.SharedKernel.Primitives;

namespace Agendio.UnitTests.SharedKernel;

public class ValueObjectTests
{
    [Fact]
    public void Two_Value_Objects_With_Same_Components_Should_Be_Equal()
    {
        var a = new Point(1, 2);
        var b = new Point(1, 2);

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Value_Objects_With_Different_Components_Should_Not_Be_Equal()
    {
        var a = new Point(1, 2);
        var b = new Point(1, 3);

        a.ShouldNotBe(b);
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void Value_Object_Should_Not_Equal_Null()
    {
        var a = new Point(1, 2);

        (a == null).ShouldBeFalse();
        (null! == a).ShouldBeFalse();
    }

    private sealed class Point(int x, int y) : ValueObject
    {
        public int X { get; } = x;
        public int Y { get; } = y;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return X;
            yield return Y;
        }
    }
}
