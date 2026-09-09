namespace JobTracker.Common.Infrastructure;

/// <summary>
/// Where the acting organization comes from. Middleware resolves it from the
/// validated <c>org</c> claim on a request; a background worker sets it from
/// the message it is processing; a test supplies it directly.
///
/// It exists so the EF global query filter has something to compare against
/// without any layer above remembering to pass a tenant — which is the whole of
/// NFR-1: isolation must not depend on a caller remembering to filter.
/// </summary>
public interface ITenantContext
{
    Guid OrganizationId { get; }
}

/// <summary>
/// How work with no request behind it declares whose data it is about.
///
/// The outbox drain runs in a background job: there is no HTTP context, no
/// principal and no claim, so every tenant-scoped query a handler makes would
/// otherwise fail — which is exactly what happened the first time the pipeline
/// ran end to end. The organization travels on the domain event, and the
/// processor sets it here before publishing.
///
/// Separate from <see cref="ITenantContext"/> on purpose: reading a tenant is
/// something every layer does, and setting one is something exactly two places
/// may do. A handler that could set its own tenant could read another's data.
/// </summary>
public interface ITenantContextSetter
{
    IDisposable Use(Guid organizationId);
}

/// <summary>
/// The test and background implementation: it starts from a fixed organization
/// and can be pointed at another for the duration of a scope.
/// </summary>
public sealed class MutableTenantContext(Guid organizationId) : ITenantContext, ITenantContextSetter
{
    private Guid _current = organizationId;

    public Guid OrganizationId => _current;

    public IDisposable Use(Guid organization)
    {
        var previous = _current;
        _current = organization;

        return new Restore(() => _current = previous);
    }

    private sealed class Restore(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }
}
