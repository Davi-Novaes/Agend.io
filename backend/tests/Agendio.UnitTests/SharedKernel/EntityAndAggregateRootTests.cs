using Agendio.SharedKernel.DomainEvents;
using Agendio.SharedKernel.Primitives;

namespace Agendio.UnitTests.SharedKernel;

public class EntityAndAggregateRootTests
{
    [Fact]
    public void Entities_With_Same_Id_And_Type_Should_Be_Equal_Even_With_Different_Field_Values()
    {
        var id = Guid.NewGuid();
        var a = new TestEntity(id) { Name = "A" };
        var b = new TestEntity(id) { Name = "B" };

        a.ShouldBe(b);
    }

    [Fact]
    public void Entities_With_Different_Ids_Should_Not_Be_Equal()
    {
        var a = new TestEntity(Guid.NewGuid());
        var b = new TestEntity(Guid.NewGuid());

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Raising_A_Domain_Event_Should_Add_It_To_The_Aggregate()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());

        aggregate.DoSomething();

        aggregate.DomainEvents.Count.ShouldBe(1);
        aggregate.DomainEvents.Single().ShouldBeOfType<SomethingHappened>();
    }

    [Fact]
    public void Clearing_Domain_Events_Should_Empty_The_Collection()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.DoSomething();

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.ShouldBeEmpty();
    }

    private sealed class TestEntity(Guid id) : Entity<Guid>(id)
    {
        public string Name { get; init; } = string.Empty;
    }

    private sealed record SomethingHappened : DomainEvent;

    private sealed class TestAggregate(Guid id) : AggregateRoot<Guid>(id)
    {
        public void DoSomething() => Raise(new SomethingHappened());
    }
}
