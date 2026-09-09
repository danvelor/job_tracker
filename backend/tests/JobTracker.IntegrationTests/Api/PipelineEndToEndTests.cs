using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using JobTracker.Common.Infrastructure;
using JobTracker.Modules.Billing.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure.Configurations;
using JobTracker.Modules.Jobs.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.IntegrationTests.Api;

/// <summary>
/// Walkthrough steps 4 and 9 (architecture 8.1), through the real HTTP
/// pipeline with every handler resolved by the application's own container.
/// The browser-level smoke against Compose is plan 5; proving the same two
/// steps here is what makes that a confirmation rather than a debugging
/// session.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PipelineEndToEndTests(PostgresFixture postgres) : IAsyncLifetime
{
    private ApiFactory _api = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory(postgres.ConnectionString);
        await _api.ResetSchemaAsync();
        await ResetBilling();
        _client = await _api.AuthenticatedClientAsync(RosterSeed.DevelopmentOrganization);
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private async Task ResetBilling()
    {
        using var scope = _api.Services.CreateScope();
        var billing = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        await billing.Database.MigrateAsync();
        await billing.Database.ExecuteSqlRawAsync("truncate billing.invoices");
    }

    /// <summary>
    /// Drains until there is nothing left, with a bound rather than a sleep.
    /// NFR-4 promises consequences arrive within seconds, so asserting on a
    /// bound is asserting on eventual consistency; sleeping a fixed interval
    /// pretends it is synchronous and fails on a slow machine.
    ///
    /// It drains rather than waiting for Hangfire's tick because the test
    /// should not depend on a clock. HangfireWiringTests covers the fact that
    /// something eventually calls this in production.
    /// </summary>
    private async Task DrainUntilQuiet()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var scope = _api.Services.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
            await processor.DrainAsync(default);

            var context = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
            var pending = await context.Database.SqlQuery<int>(
                $"""select count(*)::int as "Value" from jobs.outbox_messages where processed_on is null""")
                .SingleAsync();

            if (pending == 0)
            {
                return;
            }
        }

        var error = await Query(context => context.Database
            .SqlQuery<string?>($"""select error as "Value" from jobs.outbox_messages where processed_on is null limit 1""")
            .SingleAsync());

        throw new InvalidOperationException($"The outbox never drained: {error}");
    }

    private static Dictionary<string, object?> AValidJob() => new()
    {
        ["title"] = "Ridge tile replacement",
        ["description"] = "north slope",
        ["street"] = "12 Elm St",
        ["city"] = "Springfield",
        ["state"] = "IL",
        ["zipCode"] = "62701",
        ["latitude"] = 39.78,
        ["longitude"] = -89.65,
        ["scheduledDate"] = "2099-03-14",
        ["assigneeId"] = RosterSeed.AssigneeOrtiz,
        ["customerId"] = RosterSeed.CustomerAcme,
    };

    /// <summary>
    /// Reads outside a request, which means declaring the tenant the way the
    /// outbox drain does. Without it the query filter has no claim to read and
    /// the read throws — the same failure the pipeline itself hit, and a fair
    /// reminder that NFR-1 applies to whoever is asking, tests included.
    /// </summary>
    private async Task<T> Query<T>(Func<JobsDbContext, Task<T>> read)
    {
        using var scope = _api.Services.CreateScope();
        using var _ = scope.ServiceProvider.GetRequiredService<ITenantContextSetter>()
            .Use(RosterSeed.DevelopmentOrganization);

        return await read(scope.ServiceProvider.GetRequiredService<JobsDbContext>());
    }

    private async Task<List<string>> Recipients() =>
        await Query(context => context.Notifications
            .AsNoTracking().OrderBy(n => n.Recipient).Select(n => n.Recipient).ToListAsync());

    private async Task<int> Invoices(Guid jobId)
    {
        using var scope = _api.Services.CreateScope();
        var billing = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        return await billing.Invoices.AsNoTracking().CountAsync(invoice => invoice.JobId == jobId);
    }

    [Fact]
    public async Task Step_4_the_assignee_is_notified_when_a_job_is_created()
    {
        var created = await _client.PostAsJsonAsync("/api/jobs", AValidJob());
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        await DrainUntilQuiet();

        // Nothing in the interface shows this — design A5 step 6 says creation
        // does not wait for it — so the assertion reads the database the system
        // just wrote. That is the normal shape of an integration test, not a
        // leaked abstraction (architecture 8.1).
        (await Recipients()).Should().Equal("J. Ortiz");
    }

    [Fact]
    public async Task Step_9_an_invoice_exists_and_the_customer_was_notified()
    {
        var created = await _client.PostAsJsonAsync("/api/jobs", AValidJob());
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await DrainUntilQuiet();

        await _client.PostAsync($"/api/jobs/{id}/start", null);
        await _client.PostAsJsonAsync($"/api/jobs/{id}/complete", new
        {
            signatureUrl = "data:image/png;base64,AAA",
            photos = new[] { new { url = "p1.jpg", caption = "ridge" } },
        });

        await DrainUntilQuiet();

        (await Invoices(id)).Should().Be(1);
        (await Recipients()).Should().Equal("J. Ortiz", "ops@acme.test");
    }

    [Fact]
    public async Task The_completion_response_does_not_wait_for_either_consequence()
    {
        var created = await _client.PostAsJsonAsync("/api/jobs", AValidJob());
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await _client.PostAsync($"/api/jobs/{id}/start", null);

        var completed = await _client.PostAsJsonAsync($"/api/jobs/{id}/complete", new
        {
            signatureUrl = "data:image/png;base64,AAA",
            photos = Array.Empty<object>(),
        });

        // NFR-4 and design A5 step 6: 204 and no claim. The invoice does not
        // exist yet, and the interface never said it did.
        completed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Invoices(id)).Should().Be(0);
    }

    [Fact]
    public async Task Draining_twice_leaves_one_invoice_and_two_notifications()
    {
        var created = await _client.PostAsJsonAsync("/api/jobs", AValidJob());
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await DrainUntilQuiet();
        await _client.PostAsync($"/api/jobs/{id}/start", null);
        await _client.PostAsJsonAsync($"/api/jobs/{id}/complete", new
        {
            signatureUrl = "data:image/png;base64,AAA",
            photos = Array.Empty<object>(),
        });
        await DrainUntilQuiet();

        // A crash between a handler succeeding and processed_on being stamped
        // replays the message (4.3). This is that, and the three consumers'
        // constraints are what make it harmless.
        using (var scope = _api.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
            await context.Database.ExecuteSqlRawAsync(
                "update jobs.outbox_messages set processed_on = null");
        }

        await DrainUntilQuiet();

        (await Invoices(id)).Should().Be(1);
        (await Recipients()).Should().HaveCount(2);
    }

    [Fact]
    public async Task A_cancelled_job_neither_bills_nor_notifies_the_customer()
    {
        var created = await _client.PostAsJsonAsync("/api/jobs", AValidJob());
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await DrainUntilQuiet();

        await _client.PostAsJsonAsync($"/api/jobs/{id}/cancel", new { reason = "Weather" });
        await DrainUntilQuiet();

        // JobCancelledDomainEvent is the second internal-only event
        // (architecture 4.4). Its consequence is that there is none — which is
        // only visible as an absence, so it needs a test.
        (await Invoices(id)).Should().Be(0);
        (await Recipients()).Should().Equal("J. Ortiz");
    }
}
