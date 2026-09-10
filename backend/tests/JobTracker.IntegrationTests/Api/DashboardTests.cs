using System.Net;
using FluentAssertions;
using System.Net.Http.Headers;
using JobTracker.Api.Authentication;
using JobTracker.Modules.Jobs.Infrastructure.Configurations;

namespace JobTracker.IntegrationTests.Api;

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

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_unauthenticated_caller_cannot_even_tell_it_is_absent()
    {
        await using var production = new ApiFactory(postgres.ConnectionString, "Production");

        var response = await production.CreateClient().GetAsync("/hangfire");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_loopback_request_reaches_the_dashboard_in_development()
    {
        var response = await _api.CreateClient().GetAsync("/hangfire");

        response.IsSuccessStatusCode.Should().BeTrue(
            "the dashboard has to be reachable, or its two guards guard nothing — got {0}",
            response.StatusCode);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Hangfire");
    }

    [Fact]
    public async Task The_OpenAPI_document_is_reachable_in_development()
    {
        var response = await _api.CreateClient().GetAsync("/openapi/v1.json");

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public void The_filter_refuses_a_public_address()
    {
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

        filter.ShouldAllow(IPAddress.Parse("172.18.0.1")).Should().BeTrue();
        filter.ShouldAllow(IPAddress.Parse("10.1.2.3")).Should().BeTrue();
        filter.ShouldAllow(IPAddress.Parse("192.168.1.5")).Should().BeTrue();
    }

    [Fact]
    public void The_filter_unmaps_an_IPv4_address_that_arrived_over_a_dual_stack_socket()
    {
        var filter = new LocalOnlyDashboardFilter();

        filter.ShouldAllow(IPAddress.Parse("192.168.65.1").MapToIPv6()).Should().BeTrue();
        filter.ShouldAllow(IPAddress.Parse("172.18.0.1").MapToIPv6()).Should().BeTrue();

        filter.ShouldAllow(IPAddress.Parse("203.0.113.9").MapToIPv6()).Should().BeFalse();
    }

    [Fact]
    public void The_filter_refuses_an_address_just_outside_the_private_ranges()
    {
        var filter = new LocalOnlyDashboardFilter();

        filter.ShouldAllow(IPAddress.Parse("172.31.255.255")).Should().BeTrue();
        filter.ShouldAllow(IPAddress.Parse("172.32.0.1")).Should().BeFalse();
        filter.ShouldAllow(IPAddress.Parse("172.15.0.1")).Should().BeFalse();
        filter.ShouldAllow(IPAddress.Parse("11.0.0.1")).Should().BeFalse();
    }

    [Fact]
    public void The_filter_refuses_a_request_with_no_address_at_all()
    {
        new LocalOnlyDashboardFilter().ShouldAllow(null).Should().BeFalse();
    }
}
