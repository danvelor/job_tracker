namespace JobTracker.Modules.Jobs.Application.Abstractions;

public interface IBackgroundQueue
{
    void Enqueue<TRequest>(TRequest request, Guid organizationId) where TRequest : notnull;

    void Flush();
}
