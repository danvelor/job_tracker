using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using JobTracker.Modules.Jobs.Infrastructure.Configurations;

namespace JobTracker.IntegrationTests.Api;

[Collection(PostgresCollection.Name)]
public sealed class AuthenticationTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly Guid Organization = RosterSeed.DevelopmentOrganization;

    private ApiFactory _api = null!;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory(postgres.ConnectionString);
        await _api.ResetSchemaAsync();
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private HttpClient ClientWith(string token)
    {
        var client = _api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task A_request_with_no_token_is_refused()
    {
        var response = await _api.CreateClient().GetAsync("/api/jobs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_token_signed_by_someone_else_is_refused()
    {
        var response = await ClientWith(TestTokens.Forged(Organization)).GetAsync("/api/jobs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_expired_token_is_refused()
    {
        var response = await ClientWith(TestTokens.Expired(Organization)).GetAsync("/api/jobs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_valid_token_reaches_the_endpoint()
    {
        var client = await _api.AuthenticatedClientAsync(Organization);

        var response = await client.GetAsync("/api/jobs?limit=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_dev_token_endpoint_does_not_exist_outside_development()
    {
        await using var production = new ApiFactory(postgres.ConnectionString, "Production");
        var client = production.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Signed(Organization, ApiFactory.SigningKey));

        var response = await client.PostAsJsonAsync(
            "/auth/dev-token", new { organizationId = Organization });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_unauthenticated_caller_cannot_tell_which_routes_exist()
    {
        var missing = await _api.CreateClient().GetAsync("/api/no-such-route");
        var real = await _api.CreateClient().GetAsync("/api/jobs");

        missing.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        real.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Every_route_requires_authentication_by_default()
    {
        foreach (var route in new[] { "/api/jobs", "/api/assignees", "/api/customers" })
        {
            var response = await _api.CreateClient().GetAsync(route);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "{0} must be closed", route);
        }
    }
}
