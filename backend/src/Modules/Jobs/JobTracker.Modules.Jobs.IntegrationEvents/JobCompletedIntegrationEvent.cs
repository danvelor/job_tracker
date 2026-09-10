namespace JobTracker.Modules.Jobs.IntegrationEvents;

public sealed record JobCompletedIntegrationEvent(
    Guid EventId,
    Guid JobId,
    Guid CustomerId,
    Guid OrganizationId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
