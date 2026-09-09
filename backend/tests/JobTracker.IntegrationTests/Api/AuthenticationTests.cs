using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using JobTracker.Modules.Jobs.Infrastructure.Configurations;

namespace JobTracker.IntegrationTests.Api;

/// <summary>
/// Authentication was designed rather than assumed (architecture 7.1), because
/// multi-tenancy has nowhere else to obtain the organization from. That makes
/// it load-bearing, and it is tested as such — including the cases a
/// happy-path suite omits.
/// </summary>
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

        // The signature is the whole of the protection. Without this the API
        // would accept any well-formed JWT and the org claim would be a
        // suggestion anyone could write.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_expired_token_is_refused()
    {
        var response = await ClientWith(TestTokens.Expired(Organization)).GetAsync("/api/jobs");

        // With the default five minutes of clock skew this passes for five
        // minutes after expiry, which is five minutes of a revoked session.
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

        // Authenticated with a token built directly, because in Production
        // there is no endpoint to mint one. Without the header the answer
        // would be 401 for every unmapped path and the test would pass whether
        // or not the route existed.
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Signed(Organization, ApiFactory.SigningKey));

        var response = await client.PostAsJsonAsync(
            "/auth/dev-token", new { organizationId = Organization });

        // Architecture 7.1. An endpoint that mints a token for any
        // organization is a tenant isolation bypass with a friendly name.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_unauthenticated_caller_cannot_tell_which_routes_exist()
    {
        var missing = await _api.CreateClient().GetAsync("/api/no-such-route");
        var real = await _api.CreateClient().GetAsync("/api/jobs");

        // The fallback policy applies before a 404 can be produced, so both
        // answer 401. That is the desirable direction: probing for routes
        // without credentials tells the prober nothing.
        missing.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        real.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Every_route_requires_authentication_by_default()
    {
        // A fallback policy rather than an attribute per endpoint: the default
        // runs the other way, and one forgotten attribute is an open route.
        foreach (var route in new[] { "/api/jobs", "/api/assignees", "/api/customers" })
        {
            var response = await _api.CreateClient().GetAsync(route);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "{0} must be closed", route);
        }
    }
}
