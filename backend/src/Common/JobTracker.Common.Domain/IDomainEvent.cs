using MediatR;

namespace JobTracker.Common.Domain;

public interface IDomainEvent : INotification
{
    Guid Id { get; }

    DateTimeOffset OccurredOn { get; }

    Guid OrganizationId { get; }
}
