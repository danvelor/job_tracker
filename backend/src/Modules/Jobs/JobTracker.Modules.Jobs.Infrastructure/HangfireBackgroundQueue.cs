using Hangfire;
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
    public void Enqueue<TRequest>(TRequest request) where TRequest : notnull =>
        client.Enqueue<MediatorJobRunner>(runner => runner.RunAsync(request));
}

/// <summary>
/// Hangfire serialises the arguments of the expression it is given, so the
/// method it calls has to be on a resolvable type rather than on a closure. The
/// runner is that type, and it exists for no other reason.
/// </summary>
public sealed class MediatorJobRunner(ISender sender)
{
    public Task RunAsync(object request) => sender.Send(request);
}
