using FluentAssertions;
using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain.UnitTests.Common;

public sealed class AggregateRootTests
{
    private sealed record ThingHappened(Guid Id) : IDomainEvent;

    private sealed class Thing() : AggregateRoot(Guid.NewGuid())
    {
        public void DoSomething() => Raise(new ThingHappened(Id));
    }

    private sealed class TestEntity(Guid id) : Entity(id);

    [Fact]
    public void A_raised_event_is_visible_on_the_aggregate()
    {
        var thing = new Thing();

        thing.DoSomething();

        thing.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void The_event_collection_is_exposed_read_only()
    {
        // A caller that could add to this collection could fabricate a
        // consequence the aggregate never decided on.
        typeof(AggregateRoot)
            .GetProperty(nameof(AggregateRoot.DomainEvents))!
            .PropertyType.Should().Be(typeof(IReadOnlyCollection<IDomainEvent>));
    }

    [Fact]
    public void Clearing_empties_the_collection()
    {
        var thing = new Thing();
        thing.DoSomething();

        thing.ClearDomainEvents();

        thing.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Two_entities_of_the_same_type_with_the_same_id_are_equal()
    {
        var id = Guid.NewGuid();

        new TestEntity(id).Should().Be(new TestEntity(id));
    }

    [Fact]
    public void Entities_of_different_types_with_the_same_id_are_not_equal()
    {
        var id = Guid.NewGuid();

        new TestEntity(id).Equals(new OtherEntity(id)).Should().BeFalse();
    }

    private sealed class OtherEntity(Guid id) : Entity(id);
}
