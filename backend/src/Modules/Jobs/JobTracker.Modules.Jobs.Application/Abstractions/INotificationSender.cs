namespace JobTracker.Modules.Jobs.Application.Abstractions;

public interface INotificationSender
{
    Task<SendOutcome> SendAsync(
        string recipient, string subject, string body, CancellationToken cancellationToken = default);
}

public readonly record struct SendOutcome(bool Succeeded, string? Reason)
{
    public static SendOutcome Sent() => new(true, null);

    public static SendOutcome Refused(string reason) => new(false, reason);
}
