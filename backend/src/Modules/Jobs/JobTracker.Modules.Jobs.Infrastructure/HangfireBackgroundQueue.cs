using Hangfire;
using JobTracker.Common.Domain;
using JobTracker.Common.Infrastructure;
using JobTracker.Modules.Jobs.Application.Abstractions;
using MediatR;

namespace JobTracker.Modules.Jobs.Infrastructure;

/// <summary>
/// The adapter behind <see cref="IBackgroundQueue"/>. Hangfire persists the job
/// in Postgres before returning, so a crash between enqueue and execution loses
/// nothing, and each send gets its own retry schedule with backoff.
/// </summary>
internal sealed class HangfireBackgroundQueue(IBackgroundJobClient client) : IBackgroundQueue
{
    private readonly List<Action> _deferred = [];

    public void Enqueue<TRequest>(TRequest request, Guid organizationId)
        where TRequest : notnull =>
        _deferred.Add(() =>
            client.Enqueue<MediatorJobRunner>(runner => runner.RunAsync(request, organizationId)));

    public void Flush()
    {
        // Hangfire writes the job on its own connection, so a worker can start
        // it the instant this returns. Everything it will read has to be
        // committed by then, which is why only the owner of the transaction
        // calls this.
        foreach (var dispatch in _deferred)
        {
            dispatch();
        }

        _deferred.Clear();
    }
}

/// <summary>
/// Hangfire serialises the arguments of the expression it is given, so the
/// method it calls has to live on a resolvable type rather than on a closure.
/// The runner is that type, and it does one other thing: it declares the
/// tenant for the duration of the job.
///
/// That declaration is not optional. A job runs in a scope of its own with no
/// HttpContext, so without it every tenant-scoped query the handler makes
/// throws — which is precisely what happened: the notifications sat at Pending
/// while Hangfire retried on a backoff nobody was watching.
/// </summary>
public sealed class MediatorJobRunner(ISender sender, ITenantContextSetter tenant)
{
    public async Task RunAsync(object request, Guid organizationId)
    {
        // Scoped to the job, and restored after it. A worker reuses its scope
        // for whatever runs next, and a tenant left behind would let the
        // following job read another organization's data.
        using var _ = tenant.Use(organizationId);

        var outcome = await sender.Send(request);

        // A failed Result has to become an exception here, because Hangfire
        // judges a job by whether the method threw. Discarding it reported
        // success for work that did not happen: no retry, no log, and a
        // notification stuck at Pending with nothing saying why.
        if (outcome is Result { IsFailure: true } failure)
        {
            throw new InvalidOperationException(
                $"{request.GetType().Name} failed: {failure.Error.Code} — {failure.Error.Message}");
        }
    }
}
