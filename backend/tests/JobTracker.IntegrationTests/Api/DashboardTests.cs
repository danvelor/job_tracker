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
        //
        // Asserting success rather than "not 404 and not Forbidden", which is
        // what this said first and which a 401 satisfies. It was a 401 — the
        // fallback authorization policy applies to a request that selects no
        // endpoint, so it refused the dashboard before Hangfire's middleware
        // ever saw it. The weaker assertion let a dead feature pass, and the
        // README is what caught it by promising a URL that did not work.
        var response = await _api.CreateClient().GetAsync("/hangfire");

        response.IsSuccessStatusCode.Should().BeTrue(
            "the dashboard has to be reachable, or its two guards guard nothing — got {0}",
            response.StatusCode);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Hangfire");
    }

    [Fact]
    public async Task The_OpenAPI_document_is_reachable_in_development()
    {
        // The README points a reviewer at it. Same failure as the dashboard,
        // same cause, and it would have shipped as a broken link.
        var response = await _api.CreateClient().GetAsync("/openapi/v1.json");

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public void The_filter_refuses_a_public_address()
    {
        // The second condition. It is the weaker one — see the class comment —
        // and it is here because ASPNETCORE_ENVIRONMENT is a string in a
        // Compose file whose failure mode is silent.
        var filter = new LocalOnlyDashboardFilter();

        filter.ShouldAllow(IPAddress.Parse("203.0.113.9")).Should().BeFalse();
        filter.ShouldAllow(IPAddress.Parse("8.8.8.8")).Should().BeFalse();
        filter.ShouldAllow(IPAddress.Parse("2001:db8::1")).Should().BeFalse();
    }

    [Fact]
    public void The_filter_allows_loopback_and_the_private_network_the_stack_runs_on()
    {
        var filter = new LocalOnlyDashboardFilter();

        filter.ShouldAllow(IPAddress.Loopback).Should().BeTrue();
        filter.ShouldAllow(IPAddress.IPv6Loopback).Should().BeTrue();

        // 172.18.0.1 is not a detail invented for this test. It is the Docker
        // bridge gateway, and it is the address the container actually sees
        // when a reviewer opens a published port from the host — which
        // loopback-only refused, leaving the dashboard dead in the one
        // environment it exists for.
        filter.ShouldAllow(IPAddress.Parse("172.18.0.1")).Should().BeTrue();
        filter.ShouldAllow(IPAddress.Parse("10.1.2.3")).Should().BeTrue();
        filter.ShouldAllow(IPAddress.Parse("192.168.1.5")).Should().BeTrue();
    }

    [Fact]
    public void The_filter_unmaps_an_IPv4_address_that_arrived_over_a_dual_stack_socket()
    {
        // Kestrel binds dual-stack, so an IPv4 client arrives as
        // ::ffff:192.168.65.1 rather than as 192.168.65.1. IPAddress.IsLoopback
        // unmaps internally, which is why the dashboard worked from inside the
        // container and answered 401 from the host — the range check saw an
        // IPv6 address in no private v6 range and refused it.
        var filter = new LocalOnlyDashboardFilter();

        filter.ShouldAllow(IPAddress.Parse("192.168.65.1").MapToIPv6()).Should().BeTrue();
        filter.ShouldAllow(IPAddress.Parse("172.18.0.1").MapToIPv6()).Should().BeTrue();

        // And it must not become a way in: a mapped public address is still a
        // public address.
        filter.ShouldAllow(IPAddress.Parse("203.0.113.9").MapToIPv6()).Should().BeFalse();
    }

    [Fact]
    public void The_filter_refuses_an_address_just_outside_the_private_ranges()
    {
        // 172.16/12 ends at 172.31.255.255, and a range check written by eye
        // usually gets that boundary wrong in one direction or the other.
        var filter = new LocalOnlyDashboardFilter();

        filter.ShouldAllow(IPAddress.Parse("172.31.255.255")).Should().BeTrue();
        filter.ShouldAllow(IPAddress.Parse("172.32.0.1")).Should().BeFalse();
        filter.ShouldAllow(IPAddress.Parse("172.15.0.1")).Should().BeFalse();
        filter.ShouldAllow(IPAddress.Parse("11.0.0.1")).Should().BeFalse();
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
