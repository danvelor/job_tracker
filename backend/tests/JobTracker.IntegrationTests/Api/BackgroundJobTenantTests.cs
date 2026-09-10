using FluentAssertions;
using JobTracker.Common.Infrastructure;
using JobTracker.Modules.Jobs.Application.Notifications;
using JobTracker.Modules.Jobs.Domain;
using JobTracker.Modules.Jobs.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.IntegrationTests.Api;

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

        var read = () => scope.ServiceProvider.GetRequiredService<ITenantContext>().OrganizationId;

        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task A_send_that_fails_surfaces_the_failure_rather_than_reporting_success()
    {
        using var scope = _api.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<MediatorJobRunner>();

        var run = async () => await runner.RunAsync(
            new SendNotificationCommand(Guid.NewGuid()), Organization);

        await run.Should().ThrowAsync<InvalidOperationException>()
            .Where(exception => exception.Message.Contains("notification.not-found"));
    }
}
