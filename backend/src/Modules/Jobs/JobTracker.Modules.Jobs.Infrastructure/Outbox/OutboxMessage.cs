namespace JobTracker.Modules.Jobs.Infrastructure.Outbox;

/// <summary>
/// A serialised domain event waiting to be published. Infrastructure, not
/// domain: nothing in the model knows the outbox exists, which is what lets the
/// poller be replaced by a broker without touching a rule (D-03).
/// </summary>
internal sealed class OutboxMessage
{
    private OutboxMessage() { }

    internal OutboxMessage(Guid id, string type, string content, DateTimeOffset occurredOn)
    {
        Id = id;
        Type = type;
        Content = content;
        OccurredOn = occurredOn;
    }

    /// <summary>The event's own identity, not a fresh one (D-34).</summary>
    public Guid Id { get; private init; }

    public string Type { get; private init; } = string.Empty;
    public string Content { get; private init; } = string.Empty;
    public DateTimeOffset OccurredOn { get; private init; }
    public DateTimeOffset? ProcessedOn { get; private set; }
    public string? Error { get; private set; }

    /// <summary>
    /// Stamped only once every handler has returned (architecture 4.3).
    /// Stamping earlier turns a crash into a lost message rather than a
    /// repeated one, and repeated is what the consumers are built for.
    /// </summary>
    internal void MarkProcessed(DateTimeOffset processedOn) => ProcessedOn = processedOn;

    /// <summary>
    /// The row stays unprocessed on purpose: the error is a note for whoever
    /// reads the table, not a tombstone. The next drain tries again.
    /// </summary>
    internal void RecordFailure(string error) => Error = error;
}
