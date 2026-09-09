namespace JobTracker.Modules.Jobs.IntegrationEvents;

/// <summary>
/// Jobs' public statement, and deliberately poorer than the domain event: a
/// consumer gets identifiers and timestamps, never a <c>Job</c>. It carries
/// primitives only, and this project references nothing, so that cannot quietly
/// stop being true.
///
/// It reports the labour window rather than an amount, because Jobs does not
/// know what work costs and must not learn (D-33). Billing owns the rate. That
/// division is what makes the boundary worth having: Jobs says what happened,
/// Billing decides what it is worth.
///
/// <paramref name="EventId"/> travels so a consumer's idempotency key can
/// derive from it — the same stability argument as D-34, carried across the
/// boundary.
/// </summary>
public sealed record JobCompletedIntegrationEvent(
    Guid EventId,
    Guid JobId,
    Guid CustomerId,
    Guid OrganizationId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
