using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain;

/// <summary>
/// Public because <see cref="Job.Photos"/> is a public member and a public
/// member cannot expose an internal type (CS0053). Reachability is enforced by
/// the internal constructor and by the absence of an AddPhoto on the aggregate,
/// not by the class modifier.
/// </summary>
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
