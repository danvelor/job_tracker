using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain;

public sealed class Assignee : Entity, ITenantScoped
{
    private Assignee() { }

    public Guid OrganizationId { get; private init; }
    public string Name { get; private init; } = string.Empty;
}

public sealed class Customer : Entity, ITenantScoped
{
    private Customer() { }

    public Guid OrganizationId { get; private init; }
    public string Name { get; private init; } = string.Empty;

    public string Email { get; private init; } = string.Empty;
}

public interface IPartyRepository
{
    Task<IReadOnlyList<Assignee>> ListAssigneesAsync(
        Guid organizationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Customer>> ListCustomersAsync(
        Guid organizationId, CancellationToken cancellationToken = default);

    Task<bool> AssigneeExistsAsync(
        Guid assigneeId, Guid organizationId, CancellationToken cancellationToken = default);

    Task<bool> CustomerExistsAsync(
        Guid customerId, Guid organizationId, CancellationToken cancellationToken = default);
}
