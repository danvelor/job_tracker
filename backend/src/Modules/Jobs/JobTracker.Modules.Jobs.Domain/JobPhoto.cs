using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain;

public sealed class JobPhoto : Entity
{
    internal JobPhoto(Guid id, string url, DateTimeOffset capturedAt, string? caption)
        : base(id)
    {
        Url = url;
        CapturedAt = capturedAt;
        Caption = caption;
    }

    private JobPhoto() { }

    public string Url { get; private init; } = string.Empty;
    public DateTimeOffset CapturedAt { get; private init; }
    public string? Caption { get; private init; }
    public Guid JobId { get; private init; }
}
