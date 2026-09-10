using FluentAssertions;
using Hangfire;
using Hangfire.Storage;
using JobTracker.Modules.Jobs.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.IntegrationTests.Api;

[Collection(PostgresCollection.Name)]
public sealed class HangfireWiringTests(PostgresFixture postgres) : IAsyncLifetime
{
    private ApiFactory _api = null!;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory(postgres.ConnectionString);
        await _api.ResetSchemaAsync();
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public void The_outbox_drain_is_registered_as_a_recurring_job()
    {
        using var connection = _api.Services.GetRequiredService<JobStorage>().GetConnection();

        var recurring = connection.GetRecurringJobs();

        recurring.Select(job => job.Id).Should().Contain(OutboxDrainJob.RecurringJobId);
    }

    [Fact]
    public async Task Hangfire_keeps_its_state_in_a_schema_of_its_own()
    {
        using var scope = _api.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobsDbContext>();

        var schemas = await context.Database
            .SqlQuery<string>(
                $"""select schema_name as "Value" from information_schema.schemata""")
            .ToListAsync();

        schemas.Should().Contain("hangfire");
    }
}
