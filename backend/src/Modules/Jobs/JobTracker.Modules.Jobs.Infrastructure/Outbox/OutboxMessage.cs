namespace JobTracker.Modules.Jobs.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    private OutboxMessage() { }

    internal OutboxMessage(Guid id, string type, string content, DateTimeOffset occurredOn)
    {
        Id = id;
        Type = type;
        Content = content;
        OccurredOn = occurredOn;
    }

    public Guid Id { get; private init; }

    public string Type { get; private init; } = string.Empty;
    public string Content { get; private init; } = string.Empty;
    public DateTimeOffset OccurredOn { get; private init; }
    public DateTimeOffset? ProcessedOn { get; private set; }
    public string? Error { get; private set; }

    internal void MarkProcessed(DateTimeOffset processedOn) => ProcessedOn = processedOn;

    internal void RecordFailure(string error) => Error = error;
}
