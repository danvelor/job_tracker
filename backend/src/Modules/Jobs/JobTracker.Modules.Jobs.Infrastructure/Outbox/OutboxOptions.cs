namespace JobTracker.Modules.Jobs.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int BatchSize { get; init; } = 20;

    public int PollSeconds { get; init; } = 10;
}
