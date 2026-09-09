using System.Net;
using FluentAssertions;
using System.Net.Http.Headers;
using JobTracker.Api.Authentication;
using JobTracker.Modules.Jobs.Infrastructure.Configurations;

namespace JobTracker.IntegrationTests.Api;

/// <summary>
/// Architecture 7.5 wants the dashboard: it makes the whole async pipeline
/// inspectable without reading logs, which is worth a great deal to a reviewer.
///
/// It cannot use bearer authentication. A browser navigating to /hangfire sends
/// no Authorization header, so the fallback policy would refuse every request
/// and the feature would be dead rather than protected. So it gets two other
/// conditions, both cheap, neither depending on the other being right:
/// Development only, and loopback only.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DashboardTests(PostgresFixture postgres) : IAsyncLifetime
{
    private ApiFactory _api = null!;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory(postgres.ConnectionString);
        await _api.ResetSchemaAsync();
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task The_dashboard_is_not_registered_outside_development()
    {
        await using var production = new ApiFactory(postgres.ConnectionString, "Production");
        var client = production.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestTokens.Signed(RosterSeed.DevelopmentOrganization, ApiFactory.SigningKey));

        var response = await client.GetAsync("/hangfire");

        // Authenticated on purpose. Without a token this answers 401 like any
        // unmapped route, which proves nothing about whether the dashboard is
        // there — a valid token is what makes 404 mean "no such route" rather
        // than "you may not ask".
        //
        // An open job dashboard on a deployed host lists every job's arguments,
        // and those arguments carry organization identifiers.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_unauthenticated_caller_cannot_even_tell_it_is_absent()
    {
        await using var production = new ApiFactory(postgres.ConnectionString, "Production");

        var response = await production.CreateClient().GetAsync("/hangfire");

        // The fallback policy runs before routing can produce a 404, so probing
        // for the dashboard without credentials tells the prober nothing.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_loopback_request_reaches_the_dashboard_in_development()
    {
        // The reviewer's case: a published port from the host, or
        // `docker compose exec`. It has to actually work, or the second
        // condition would be indistinguishable from having removed it.
        var response = await _api.CreateClient().GetAsync("/hangfire");

        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public void The_filter_refuses_an_address_that_is_not_loopback()
    {
        // ASPNETCORE_ENVIRONMENT is a string in a Compose file and its failure
        // mode is silent, so Development-only is not enough on its own. This is
        // the condition that does not depend on somebody getting that right.
        var filter = new LocalOnlyDashboardFilter();

        filter.ShouldAllow(System.Net.IPAddress.Parse("203.0.113.9")).Should().BeFalse();
        filter.ShouldAllow(System.Net.IPAddress.Loopback).Should().BeTrue();
        filter.ShouldAllow(System.Net.IPAddress.IPv6Loopback).Should().BeTrue();
    }

    [Fact]
    public void The_filter_refuses_a_request_with_no_address_at_all()
    {
        // A request whose remote address the server could not determine is not
        // evidence of being local. Defaulting to allow would make an unknown
        // into a permission.
        new LocalOnlyDashboardFilter().ShouldAllow(null).Should().BeFalse();
    }
}
