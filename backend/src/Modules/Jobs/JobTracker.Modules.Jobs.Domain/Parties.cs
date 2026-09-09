using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain;

/// <summary>
/// A read-only roster (D-26). No factory and no mutating method: EF
/// materialises one and nothing else writes one. It exists because three
/// controls in the interface need something to offer, and a row showing a UUID
/// is useless.
/// </summary>
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

    /// <summary>FR-10 needs somewhere to send the completion notice.</summary>
    public string Email { get; private init; } = string.Empty;
}

/// <summary>Reads only, because D-26 gives the rosters no write path.</summary>
public interface IPartyRepository
{
    Task<IReadOnlyList<Assignee>> ListAssigneesAsync(
        Guid organizationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Customer>> ListCustomersAsync(
        Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Membership, not existence. The foreign key already answers "does this
    /// row exist" and answers it across every tenant; what a command needs to
    /// know is whether the crew member is on <em>this</em> organization's
    /// roster.
    /// </summary>
    Task<bool> AssigneeExistsAsync(
        Guid assigneeId, Guid organizationId, CancellationToken cancellationToken = default);

    Task<bool> CustomerExistsAsync(
        Guid customerId, Guid organizationId, CancellationToken cancellationToken = default);
}
