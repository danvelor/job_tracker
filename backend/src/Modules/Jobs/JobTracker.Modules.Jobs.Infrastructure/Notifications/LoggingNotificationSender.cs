using JobTracker.Modules.Jobs.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace JobTracker.Modules.Jobs.Infrastructure.Notifications;

internal sealed class LoggingNotificationSender(ILogger<LoggingNotificationSender> logger)
    : INotificationSender
{
    public Task<SendOutcome> SendAsync(
        string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Notification sent to {Recipient} with subject {Subject}", recipient, subject);

        return Task.FromResult(SendOutcome.Sent());
    }
}
