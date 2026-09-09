namespace JobTracker.Modules.Jobs.Application.Abstractions;

/// <summary>
/// The port. Delivery is simulated by the only adapter there is (D-08), and the
/// port is what makes that a swap rather than a rewrite: line 241 names
/// SendGrid, and an SMTP adapter would implement this and change nothing else.
/// </summary>
public interface INotificationSender
{
    Task<SendOutcome> SendAsync(
        string recipient, string subject, string body, CancellationToken cancellationToken = default);
}

/// <summary>
/// A result, not an exception. A transport refusing is expected, and the
/// notification record has a Failed state precisely because it happens.
/// </summary>
public readonly record struct SendOutcome(bool Succeeded, string? Reason)
{
    public static SendOutcome Sent() => new(true, null);

    public static SendOutcome Refused(string reason) => new(false, reason);
}
