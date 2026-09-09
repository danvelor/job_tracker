namespace JobTracker.Modules.Jobs.Domain;

/// <summary>
/// The input to <see cref="Job.Complete"/>. A parameter shape, not an entity:
/// the aggregate turns each into a <see cref="JobPhoto"/> and assigns its
/// identity.
/// </summary>
public readonly record struct NewJobPhoto(string Url, DateTimeOffset CapturedAt, string? Caption);
