using FluentAssertions;
using JobTracker.Common.Infrastructure;
using JobTracker.Modules.Jobs.Application.Notifications;
using JobTracker.Modules.Jobs.Domain;
using JobTracker.Modules.Jobs.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.IntegrationTests.Api;

/// <summary>
/// A fire-and-forget job runs in a scope of its own, minutes after the request
/// that queued it, with no HttpContext and therefore no claim. Every earlier
/// test ran the send inline on a context whose tenant was already set, so none
/// of them could see this — it took the Compose stack, where the notifications
/// stayed Pending and Hangfire recorded "No organization claim on a validated
/// principal" against a retry schedule.
///
/// These tests resolve the runner the way Hangfire does: from a fresh scope,
/// with nothing set.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class BackgroundJobTenantTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly Guid Organization = RosterSeed.DevelopmentOrganization;

    private ApiFactory _api = null!;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory(postgres.ConnectionString);
        await _api.ResetSchemaAsync();
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private async Task<Guid> ANotification()
    {
        using var scope = _api.Services.CreateScope();
        using var _ = scope.ServiceProvider.GetRequiredService<ITenantContextSetter>()
            .Use(Organization);

        var context = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
        var notification = Notification.Draft(
            Guid.NewGuid(), Organization, "J. Ortiz", "A job has been assigned to you",
            "Ridge tile replacement", DateTimeOffset.UtcNow).Value;

        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        return notification.Id;
    }

    private async Task<NotificationStatus> StatusOf(Guid id)
    {
        using var scope = _api.Services.CreateScope();
        using var _ = scope.ServiceProvider.GetRequiredService<ITenantContextSetter>()
            .Use(Organization);

        return (await scope.ServiceProvider.GetRequiredService<JobsDbContext>()
            .Notifications.AsNoTracking().SingleAsync(n => n.Id == id)).Status;
    }

    [Fact]
    public async Task A_queued_send_runs_outside_a_request_and_still_finds_its_tenant()
    {
        var id = await ANotification();

        using (var scope = _api.Services.CreateScope())
        {
            // No ITenantContextSetter.Use here, and that is the point: this is
            // the scope Hangfire builds, and nothing in it knows a tenant until
            // the runner is told one.
            var runner = scope.ServiceProvider.GetRequiredService<MediatorJobRunner>();
            await runner.RunAsync(new SendNotificationCommand(id), Organization);
        }

        (await StatusOf(id)).Should().Be(NotificationStatus.Sent);
    }

    [Fact]
    public async Task A_queued_send_does_not_leak_its_tenant_into_the_next_job()
    {
        var id = await ANotification();

        using var scope = _api.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<MediatorJobRunner>();
        await runner.RunAsync(new SendNotificationCommand(id), Organization);

        // The scope is shared by whatever runs next on this worker. A tenant
        // left behind would let the following job read another organization's
        // data — the failure mode NFR-1 exists to prevent, arriving through the
        // fix for a different one.
        var read = () => scope.ServiceProvider.GetRequiredService<ITenantContext>().OrganizationId;

        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task A_send_that_fails_surfaces_the_failure_rather_than_reporting_success()
    {
        using var scope = _api.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<MediatorJobRunner>();

        // A notification that does not exist. The handler returns a failed
        // Result, and a runner that discarded it would tell Hangfire the job
        // succeeded — no retry, no log, and a notification stuck at Pending
        // with nothing anywhere saying why. That is how the enqueue race below
        // stayed invisible.
        var run = async () => await runner.RunAsync(
            new SendNotificationCommand(Guid.NewGuid()), Organization);

        await run.Should().ThrowAsync<InvalidOperationException>()
            .Where(exception => exception.Message.Contains("notification.not-found"));
    }
}
