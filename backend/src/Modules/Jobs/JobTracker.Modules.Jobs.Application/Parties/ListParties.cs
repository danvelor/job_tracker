using JobTracker.Common.Domain;
using JobTracker.Modules.Jobs.Domain;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Parties;

public sealed record PartyResponse(Guid Id, string Name);

public sealed record ListAssigneesQuery(Guid OrganizationId)
    : IRequest<Result<IReadOnlyList<PartyResponse>>>;

public sealed record ListCustomersQuery(Guid OrganizationId)
    : IRequest<Result<IReadOnlyList<PartyResponse>>>;

internal sealed class ListAssigneesQueryHandler(IPartyRepository parties)
    : IRequestHandler<ListAssigneesQuery, Result<IReadOnlyList<PartyResponse>>>
{
    public async Task<Result<IReadOnlyList<PartyResponse>>> Handle(
        ListAssigneesQuery query, CancellationToken cancellationToken)
    {
        var assignees = await parties.ListAssigneesAsync(query.OrganizationId, cancellationToken);

        return Result.Success<IReadOnlyList<PartyResponse>>(
            assignees.Select(assignee => new PartyResponse(assignee.Id, assignee.Name)).ToList());
    }
}

internal sealed class ListCustomersQueryHandler(IPartyRepository parties)
    : IRequestHandler<ListCustomersQuery, Result<IReadOnlyList<PartyResponse>>>
{
    public async Task<Result<IReadOnlyList<PartyResponse>>> Handle(
        ListCustomersQuery query, CancellationToken cancellationToken)
    {
        var customers = await parties.ListCustomersAsync(query.OrganizationId, cancellationToken);

        return Result.Success<IReadOnlyList<PartyResponse>>(
            customers.Select(customer => new PartyResponse(customer.Id, customer.Name)).ToList());
    }
}
