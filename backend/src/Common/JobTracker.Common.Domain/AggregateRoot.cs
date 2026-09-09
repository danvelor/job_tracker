namespace JobTracker.Common.Domain;

public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(Guid id) : base(id) { }

    protected AggregateRoot() { }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>
    /// Called by the outbox interceptor after it has copied the events into the
    /// same transaction as the state change (architecture 4.2).
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
