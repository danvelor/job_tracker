namespace JobTracker.Common.Domain;

/// <summary>
/// The identity every domain event carries (D-34).
///
/// It is generated once, when the aggregate raises the event, and frozen by
/// serialisation into the outbox — so a message replayed after a crash arrives
/// with the identity it had the first time. That stability is the whole
/// condition architecture 4.5 puts on an idempotency key, and it is why a
/// consumer can key off this value with no ambient context telling it which
/// outbox row carried it.
///
/// <see cref="OccurredOn"/> reads a clock, which the rule about time being a
/// parameter otherwise forbids. That rule protects business decisions from an
/// untestable clock; this is a record of when something was written, no rule
/// ever reads it, and threading `now` into every event's initialiser would add
/// a parameter for no test's benefit.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Whose data this event is about. It travels because the outbox drain has
    /// no request and therefore no claim behind it: without this, every
    /// tenant-scoped query a handler makes would have nothing to filter by.
    /// </summary>
    public Guid OrganizationId { get; init; }
}
