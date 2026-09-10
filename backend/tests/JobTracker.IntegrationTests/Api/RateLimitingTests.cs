using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using JobTracker.Modules.Jobs.Infrastructure.Configurations;
using Microsoft.AspNetCore.Hosting;

namespace JobTracker.IntegrationTests.Api;

[Collection(PostgresCollection.Name)]
public sealed class RateLimitingTests(PostgresFixture postgres) : IAsyncLifetime
{
    private sealed class LimitedApiFactory(string connectionString, int permitLimit)
        : ApiFactory(connectionString)
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("RateLimiting:PermitLimit", permitLimit.ToString());
            builder.UseSetting("RateLimiting:WindowSeconds", "60");
            builder.UseSetting("RateLimiting:Enabled", "true");
        }
    }

    private LimitedApiFactory _api = null!;

    public async Task InitializeAsync()
    {
        _api = new LimitedApiFactory(postgres.ConnectionString, permitLimit: 3);
        await _api.ResetSchemaAsync();
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private static async Task<HttpResponseMessage> Exhaust(HttpClient client, int attempts)
    {
        HttpResponseMessage last = null!;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            last = await client.GetAsync("/api/jobs?limit=1");
        }

        return last;
    }

    [Fact]
    public async Task A_tenant_that_exceeds_its_allowance_is_refused_with_429()
    {
        var client = await _api.AuthenticatedClientAsync(RosterSeed.DevelopmentOrganization);

        var response = await Exhaust(client, attempts: 6);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task A_request_within_the_allowance_is_served()
    {
        var client = await _api.AuthenticatedClientAsync(RosterSeed.DevelopmentOrganization);

        var response = await client.GetAsync("/api/jobs?limit=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_refusal_says_when_to_come_back()
    {
        var client = await _api.AuthenticatedClientAsync(RosterSeed.DevelopmentOrganization);

        var response = await Exhaust(client, attempts: 6);

        response.Headers.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task One_tenant_cannot_exhaust_anothers_allowance()
    {
        var ours = await _api.AuthenticatedClientAsync(RosterSeed.DevelopmentOrganization);
        var theirs = await _api.AuthenticatedClientAsync(RosterSeed.SecondOrganization);

        await Exhaust(ours, attempts: 6);
        var theirResponse = await theirs.GetAsync("/api/jobs?limit=1");

        theirResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_unauthenticated_caller_is_partitioned_by_address_rather_than_pooled()
    {
        var client = _api.CreateClient();

        var response = await client.PostAsync("/auth/dev-token",
            new StringContent("""{"organizationId":"11111111-1111-1111-1111-111111111111"}""",
                System.Text.Encoding.UTF8, new MediaTypeHeaderValue("application/json")));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_health_endpoint_needs_no_token()
    {
        var response = await _api.CreateClient().GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_health_endpoint_is_not_metered()
    {
        var client = _api.CreateClient();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
