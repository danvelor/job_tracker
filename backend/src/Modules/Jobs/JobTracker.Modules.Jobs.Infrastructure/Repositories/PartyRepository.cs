using JobTracker.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Modules.Jobs.Infrastructure.Repositories;

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

    public Task<bool> AssigneeExistsAsync(
        Guid assigneeId, Guid organizationId, CancellationToken cancellationToken = default) =>
        context.Assignees.AsNoTracking()
            .AnyAsync(assignee => assignee.Id == assigneeId, cancellationToken);

    public Task<bool> CustomerExistsAsync(
        Guid customerId, Guid organizationId, CancellationToken cancellationToken = default) =>
        context.Customers.AsNoTracking()
            .AnyAsync(customer => customer.Id == customerId, cancellationToken);
}
