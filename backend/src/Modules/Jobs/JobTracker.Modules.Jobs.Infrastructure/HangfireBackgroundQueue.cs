using Hangfire;
using JobTracker.Common.Domain;
using JobTracker.Common.Infrastructure;
using JobTracker.Modules.Jobs.Application.Abstractions;
using MediatR;

namespace JobTracker.Modules.Jobs.Infrastructure;

internal sealed class HangfireBackgroundQueue(IBackgroundJobClient client) : IBackgroundQueue
{
    private readonly List<Action> _deferred = [];

    public void Enqueue<TRequest>(TRequest request, Guid organizationId)
        where TRequest : notnull =>
        _deferred.Add(() =>
            client.Enqueue<MediatorJobRunner>(runner => runner.RunAsync(request, organizationId)));

    public void Flush()
    {
        foreach (var dispatch in _deferred)
        {
            dispatch();
        }

        _deferred.Clear();
    }
}

public sealed class MediatorJobRunner(ISender sender, ITenantContextSetter tenant)
{
    public async Task RunAsync(object request, Guid organizationId)
    {
        using var _ = tenant.Use(organizationId);

        var outcome = await sender.Send(request);

        if (outcome is Result { IsFailure: true } failure)
        {
            throw new InvalidOperationException(
                $"{request.GetType().Name} failed: {failure.Error.Code} — {failure.Error.Message}");
        }
    }
}
