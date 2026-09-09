namespace JobTracker.Common.Infrastructure;

/// <summary>
/// Where the acting organization comes from. Middleware resolves it from the
/// validated <c>org</c> claim on a request; a test supplies it directly.
///
/// It exists so the EF global query filter has something to compare against
/// without any layer above remembering to pass a tenant — which is the whole of
/// NFR-1: isolation must not depend on a caller remembering to filter.
/// </summary>
public interface ITenantContext
{
    Guid OrganizationId { get; }
}

public sealed class FixedTenantContext(Guid organizationId) : ITenantContext
{
    public Guid OrganizationId { get; } = organizationId;
}
