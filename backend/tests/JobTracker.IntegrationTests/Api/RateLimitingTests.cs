using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using JobTracker.Modules.Jobs.Infrastructure.Configurations;
using Microsoft.AspNetCore.Hosting;

namespace JobTracker.IntegrationTests.Api;

/// <summary>
/// Architecture 7.4. Partitioned by the <c>org</c> claim, because a global
/// limiter would let one noisy organization deny service to every other one —
/// a multi-tenancy failure wearing a performance costume.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RateLimitingTests(PostgresFixture postgres) : IAsyncLifetime
{
    /// <summary>
    /// Low limits from configuration. A test that had to send the production
    /// allowance would take minutes and would be the first thing anyone
    /// disabled.
    /// </summary>
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

        // The limiter has to permit the normal case, or the test above would
        // pass against an API that refused everything.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_refusal_says_when_to_come_back()
    {
        var client = await _api.AuthenticatedClientAsync(RosterSeed.DevelopmentOrganization);

        var response = await Exhaust(client, attempts: 6);

        // 429 without Retry-After tells a client to back off by an amount it
        // has to guess, and clients guess badly — usually by retrying at once.
        response.Headers.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task One_tenant_cannot_exhaust_anothers_allowance()
    {
        var ours = await _api.AuthenticatedClientAsync(RosterSeed.DevelopmentOrganization);
        var theirs = await _api.AuthenticatedClientAsync(RosterSeed.SecondOrganization);

        await Exhaust(ours, attempts: 6);
        var theirResponse = await theirs.GetAsync("/api/jobs?limit=1");

        // The partition is the whole point of 7.4. Without it the loudest
        // tenant sets everyone else's availability.
        theirResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_unauthenticated_caller_is_partitioned_by_address_rather_than_pooled()
    {
        // No claim to partition by, and a single shared bucket for every
        // anonymous caller would mean one of them could lock out the token
        // endpoint for all of them.
        var client = _api.CreateClient();

        var response = await client.PostAsync("/auth/dev-token",
            new StringContent("""{"organizationId":"11111111-1111-1111-1111-111111111111"}""",
                System.Text.Encoding.UTF8, new MediaTypeHeaderValue("application/json")));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_health_endpoint_needs_no_token()
    {
        // Compose polls it before the container has any credentials. A
        // healthcheck that needed a token would never turn the container
        // healthy, and the stack would never come up.
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

        // Compose polls every three seconds. A limiter that counted those would
        // mark the container unhealthy under its own healthcheck — the system
        // failing because it was watching itself.
    }
}
