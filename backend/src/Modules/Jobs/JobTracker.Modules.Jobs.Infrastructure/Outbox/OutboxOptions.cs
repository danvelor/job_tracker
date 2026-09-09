namespace JobTracker.Modules.Jobs.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>
    /// How many rows one drain takes. Small enough that a batch finishes well
    /// inside the poll interval — a drain still running when the next one fires
    /// just means the second finds the rows locked and skips them, which is
    /// correct but wasted work.
    /// </summary>
    public int BatchSize { get; init; } = 20;

    /// <summary>
    /// NFR-4 promises consequences arrive within seconds, and this is the
    /// number that promise is made of.
    /// </summary>
    public int PollSeconds { get; init; } = 10;
}
