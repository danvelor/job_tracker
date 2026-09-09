using MediatR;

namespace JobTracker.Common.Domain;

/// <summary>
/// A marker. It is a MediatR notification so the outbox can publish it — the
/// one place the kernel takes a package dependency, because a marker of our
/// own plus an adapter would buy nothing.
/// </summary>
public interface IDomainEvent : INotification;
