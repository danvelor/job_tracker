namespace JobTracker.Modules.Jobs.Domain;

public readonly record struct NewJobPhoto(string Url, DateTimeOffset CapturedAt, string? Caption);
