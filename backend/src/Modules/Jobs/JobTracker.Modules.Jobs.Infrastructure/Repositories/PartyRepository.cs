using JobTracker.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Modules.Jobs.Infrastructure.Repositories;

/// <summary>
/// Two reads and no writes, because D-26 gives the rosters no write path.
///
/// <paramref name="organizationId"/> goes unused in both methods: the global
/// query filter has already applied it. It stays in the signature because
/// <see cref="IPartyRepository"/> is a domain contract and a contract that
/// hides what scopes an operation is a contract that misleads — and because the
/// day this becomes a cross-tenant admin read, the parameter is already there.
/// </summary>
internal sealed class PartyRepository(JobsDbContext context) : IPartyRepository
{
    public async Task<IReadOnlyList<Assignee>> ListAssigneesAsync(
        Guid organizationId, CancellationToken cancellationToken = default) =>
        await context.Assignees.AsNoTracking()
            .OrderBy(assignee => assignee.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Customer>> ListCustomersAsync(
        Guid organizationId, CancellationToken cancellationToken = default) =>
        await context.Customers.AsNoTracking()
            .OrderBy(customer => customer.Name)
            .ToListAsync(cancellationToken);
}
