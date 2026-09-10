namespace JobTracker.Common.Infrastructure;

public interface ITenantContext
{
    Guid OrganizationId { get; }
}

public interface ITenantContextSetter
{
    IDisposable Use(Guid organizationId);
}

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
