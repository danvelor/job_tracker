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
    public async Task A_cancelled_job_tells_the_crew_but_never_bills()
    {
        var created = await _client.PostAsJsonAsync("/api/jobs", AValidJob());
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await DrainUntilQuiet();

        await _client.PostAsJsonAsync($"/api/jobs/{id}/cancel", new { reason = "Weather" });
        await DrainUntilQuiet();

        (await Invoices(id)).Should().Be(0);
        (await Recipients()).Should().Equal("J. Ortiz", "J. Ortiz");
        (await Recipients()).Should().NotContain(recipient => recipient.Contains("@"));
    }
}
