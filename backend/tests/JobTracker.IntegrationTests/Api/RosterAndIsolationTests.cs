using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using JobTracker.Modules.Jobs.Infrastructure.Configurations;

namespace JobTracker.IntegrationTests.Api;

/// <summary>
/// Plan 3B proved the query filter confines a query. This proves the claim
/// confines the filter — the half of the chain a repository test cannot reach,
/// because it starts at a token.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RosterAndIsolationTests(PostgresFixture postgres) : IAsyncLifetime
{
    private ApiFactory _api = null!;
    private HttpClient _ours = null!;
    private HttpClient _theirs = null!;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory(postgres.ConnectionString);
        await _api.ResetSchemaAsync();
        _ours = await _api.AuthenticatedClientAsync(RosterSeed.DevelopmentOrganization);
        _theirs = await _api.AuthenticatedClientAsync(RosterSeed.SecondOrganization);
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private static Dictionary<string, object?> AJob(Guid assignee, Guid customer) => new()
    {
        ["title"] = "Ridge tile replacement",
        ["description"] = null,
        ["street"] = "12 Elm St",
        ["city"] = "Springfield",
        ["state"] = "IL",
        ["zipCode"] = "62701",
        ["latitude"] = 39.78,
        ["longitude"] = -89.65,
        ["scheduledDate"] = "2099-03-14",
        ["assigneeId"] = assignee,
        ["customerId"] = customer,
    };

    private static async Task<JsonElement> Body(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    // ---- rosters ---------------------------------------------------------

    [Fact]
    public async Task The_assignee_roster_carries_the_names_the_pickers_need()
    {
        var assignees = await Body(await _ours.GetAsync("/api/assignees"));

        assignees.EnumerateArray().Select(party => party.GetProperty("name").GetString())
            .Should().BeEquivalentTo(["J. Ortiz", "M. Ruiz"]);
    }

    [Fact]
    public async Task The_customer_roster_carries_the_names_the_pickers_need()
    {
        var customers = await Body(await _ours.GetAsync("/api/customers"));

        customers.EnumerateArray().Select(party => party.GetProperty("name").GetString())
            .Should().BeEquivalentTo(["Acme Holdings", "Birch Property"]);
    }

    [Fact]
    public async Task A_second_tenant_sees_its_own_roster_and_not_ours()
    {
        var assignees = await Body(await _theirs.GetAsync("/api/assignees"));

        assignees.GetArrayLength().Should().Be(1);
        assignees[0].GetProperty("name").GetString().Should().Be("K. Lawson");
    }

    [Fact]
    public async Task There_is_no_way_to_write_a_roster()
    {
        var response = await _ours.PostAsJsonAsync("/api/assignees", new { name = "Nobody" });

        // D-26: read-only. 405 rather than 404 is the honest answer — the
        // route exists and the verb does not.
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    // ---- isolation over the wire -----------------------------------------

    [Fact]
    public async Task A_job_created_under_one_token_is_invisible_under_another()
    {
        var created = await _ours.PostAsJsonAsync(
            "/api/jobs", AJob(RosterSeed.AssigneeOrtiz, RosterSeed.CustomerAcme));
        var location = created.Headers.Location!.ToString();

        var theirRead = await _theirs.GetAsync(location);

        // The whole chain: token to claim to tenant context to query filter to
        // row. Plan 3B proved the last link; this proves the first three.
        theirRead.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_transition_cannot_reach_another_tenants_job()
    {
        var created = await _ours.PostAsJsonAsync(
            "/api/jobs", AJob(RosterSeed.AssigneeOrtiz, RosterSeed.CustomerAcme));
        var id = (await Body(created)).GetProperty("id").GetGuid();

        var response = await _theirs.PostAsync($"/api/jobs/{id}/start", null);

        // 404, not 409 and not 204. A write is the dangerous direction: a
        // competitor able to start a job they cannot read is worse than one
        // able to read it.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_list_never_mixes_two_tenants()
    {
        await _ours.PostAsJsonAsync(
            "/api/jobs", AJob(RosterSeed.AssigneeOrtiz, RosterSeed.CustomerAcme));
        await _theirs.PostAsJsonAsync(
            "/api/jobs", AJob(RosterSeed.AssigneeOther, RosterSeed.CustomerOther));

        var ourItems = (await Body(await _ours.GetAsync("/api/jobs?limit=50")))
            .GetProperty("items");
        var theirItems = (await Body(await _theirs.GetAsync("/api/jobs?limit=50")))
            .GetProperty("items");

        ourItems.GetArrayLength().Should().Be(1);
        theirItems.GetArrayLength().Should().Be(1);
        ourItems[0].GetProperty("assigneeName").GetString().Should().Be("J. Ortiz");
        theirItems[0].GetProperty("assigneeName").GetString().Should().Be("K. Lawson");
    }

    [Fact]
    public async Task A_job_cannot_be_created_against_another_tenants_roster()
    {
        // The forgery the claim cannot stop on its own: our token, their
        // assignee. The tenant filter hides the row from the foreign-key
        // check, so this must fail rather than create a job assigned to
        // someone in another company.
        var response = await _ours.PostAsJsonAsync(
            "/api/jobs", AJob(RosterSeed.AssigneeOther, RosterSeed.CustomerOther));

        response.StatusCode.Should().NotBe(HttpStatusCode.Created);
    }
}
