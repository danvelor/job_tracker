namespace JobTracker.Common.Domain;

/// <summary>
/// A one-member interface, and that is the point: an entity declares that it is
/// tenant-scoped and nothing else. An architecture test asserts every
/// implementer has a query filter configured, so adding a table cannot silently
/// omit the protection — layer 4 of architecture 7.2.
/// </summary>
public interface ITenantScoped
{
    Guid OrganizationId { get; }
}
