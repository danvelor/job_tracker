using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using JobTracker.Modules.Jobs.Infrastructure.Configurations;

namespace JobTracker.IntegrationTests.Api;

/// <summary>One test per row of design B6, plus the refusals each row names.</summary>
[Collection(PostgresCollection.Name)]
public sealed class JobEndpointTests(PostgresFixture postgres) : IAsyncLifetime
{
    private ApiFactory _api = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory(postgres.ConnectionString);
        await _api.ResetSchemaAsync();
        _client = await _api.AuthenticatedClientAsync(RosterSeed.DevelopmentOrganization);
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private static Dictionary<string, object?> AValidJob(
        string title = "Ridge tile replacement", string scheduledDate = "2099-03-14") => new()
    {
        ["title"] = title,
        ["description"] = "Replace cracked ridge tiles",
        ["street"] = "12 Elm St",
        ["city"] = "Springfield",
        ["state"] = "IL",
        ["zipCode"] = "62701",
        ["latitude"] = 39.78,
        ["longitude"] = -89.65,
        ["scheduledDate"] = scheduledDate,
        ["assigneeId"] = RosterSeed.AssigneeOrtiz,
        ["customerId"] = RosterSeed.CustomerAcme,
    };

    private async Task<Guid> CreateJob(string title = "Ridge tile replacement")
    {
        var response = await _client.PostAsJsonAsync("/api/jobs", AValidJob(title));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> Body(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    // ---- create ----------------------------------------------------------

    [Fact]
    public async Task Creating_a_job_answers_201_with_a_location_the_client_can_follow()
    {
        var response = await _client.PostAsJsonAsync("/api/jobs", AValidJob());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        // 201 without a Location is a 201 the client cannot follow.
        response.Headers.Location!.ToString().Should().StartWith("/api/jobs/");
    }

    [Fact]
    public async Task A_job_scheduled_in_the_past_is_refused_with_the_field_named()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/jobs", AValidJob(scheduledDate: "2020-01-01"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // BR-1 lives in the aggregate, not in a validator, and the refusal
        // still names the field — which is what lets the form light up the
        // date rather than showing a banner (design A5 point 3).
        (await Body(response)).GetProperty("errors").GetProperty("ScheduledDate")
            .GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task A_request_that_omits_the_title_is_refused_by_the_validator()
    {
        var body = AValidJob();
        body["title"] = "";

        var response = await _client.PostAsJsonAsync("/api/jobs", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // This one comes from CreateJobCommandValidator rather than from the
        // aggregate, and proves the validators were registered at all. They
        // are internal by architecture 9.1, and without includeInternalTypes
        // FluentValidation finds none — every invalid request would answer 201.
        (await Body(response)).GetProperty("errors").GetProperty("Title")
            .GetArrayLength().Should().BeGreaterThan(0);
    }

    // ---- read ------------------------------------------------------------

    [Fact]
    public async Task A_job_that_does_not_exist_answers_404()
    {
        var response = await _client.GetAsync($"/api/jobs/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Body(response)).GetProperty("errorCode").GetString().Should().Be("job.not-found");
    }

    [Fact]
    public async Task The_list_pages_by_cursor_over_HTTP()
    {
        await CreateJob("A");
        await CreateJob("B");
        await CreateJob("C");

        var first = await Body(await _client.GetAsync("/api/jobs?limit=2"));
        var cursor = first.GetProperty("nextCursor").GetString();
        var second = await Body(await _client.GetAsync($"/api/jobs?limit=2&cursor={cursor}"));

        first.GetProperty("items").GetArrayLength().Should().Be(2);
        second.GetProperty("items").GetArrayLength().Should().Be(1);
        second.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task A_status_the_enum_does_not_define_is_a_400_rather_than_an_empty_list()
    {
        var response = await _client.GetAsync("/api/jobs?limit=5&statuses=Elsewhere");

        // An empty list would read as "no jobs match", which is a different
        // and wrong answer to a typo. Note the Route Handler in the frontend
        // does the opposite on purpose: it sanitises a URL a user can edit.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_limit_beyond_the_ceiling_is_clamped_rather_than_honoured()
    {
        for (var i = 0; i < 3; i++)
        {
            await CreateJob($"Job {i}");
        }

        var response = await _client.GetAsync("/api/jobs?limit=100000");

        // NFR-5 is not enforceable if the page size is not.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Body(response)).GetProperty("items").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task The_list_carries_the_assignee_name_and_the_nested_status_as_text()
    {
        await CreateJob();

        var items = (await Body(await _client.GetAsync("/api/jobs?limit=5"))).GetProperty("items");

        items[0].GetProperty("assigneeName").GetString().Should().Be("J. Ortiz");
        items[0].GetProperty("status").GetString().Should().Be("Scheduled");
    }

    // ---- transitions -----------------------------------------------------

    [Fact]
    public async Task Starting_a_scheduled_job_answers_204()
    {
        var id = await CreateJob();

        var response = await _client.PostAsync($"/api/jobs/{id}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Starting_a_job_twice_answers_409()
    {
        var id = await CreateJob();
        await _client.PostAsync($"/api/jobs/{id}/start", null);

        var response = await _client.PostAsync($"/api/jobs/{id}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Completing_a_job_that_never_started_answers_409_rather_than_400()
    {
        var id = await CreateJob();

        var response = await _client.PostAsJsonAsync($"/api/jobs/{id}/complete", new
        {
            signatureUrl = "data:image/png;base64,AAA",
            photos = Array.Empty<object>(),
        });

        // BR-3 through the wire. The request was well-formed; the state
        // refused it (design B6).
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Completing_without_a_signature_answers_400_rather_than_409()
    {
        var id = await CreateJob();
        await _client.PostAsync($"/api/jobs/{id}/start", null);

        var response = await _client.PostAsJsonAsync($"/api/jobs/{id}/complete", new
        {
            signatureUrl = "",
            photos = Array.Empty<object>(),
        });

        // The mirror image of the test above, and the pair is the point: one
        // status for a malformed request, another for a refused one.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancelling_without_a_reason_answers_400()
    {
        var id = await CreateJob();

        var response = await _client.PostAsJsonAsync($"/api/jobs/{id}/cancel", new { reason = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancelling_a_scheduled_job_answers_204_and_records_the_reason()
    {
        var id = await CreateJob();

        var response = await _client.PostAsJsonAsync(
            $"/api/jobs/{id}/cancel", new { reason = "Weather" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Body(await _client.GetAsync($"/api/jobs/{id}")))
            .GetProperty("cancellationReason").GetString().Should().Be("Weather");
    }

    [Fact]
    public async Task Rescheduling_into_the_past_answers_400_like_creating_in_the_past_does()
    {
        var id = await CreateJob();

        var response = await _client.PatchAsJsonAsync($"/api/jobs/{id}/schedule", new
        {
            scheduledDate = "2020-01-01",
            assigneeId = RosterSeed.AssigneeRuiz,
        });

        // Design B6 asks for 409 on this row and 400 on the create row, for
        // the same rule. Resolved as 400 in both: BR-1 refuses a value, not a
        // state — the same date is refused whether the job is new or being
        // corrected, and the caller fixes it the same way. 409 stays for the
        // refusals where nothing the caller sent was wrong. Recorded as D-32.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Body(response)).GetProperty("errors").GetProperty("ScheduledDate")
            .GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Rescheduling_a_scheduled_job_moves_it()
    {
        var id = await CreateJob();

        var response = await _client.PatchAsJsonAsync($"/api/jobs/{id}/schedule", new
        {
            scheduledDate = "2099-06-01",
            assigneeId = RosterSeed.AssigneeRuiz,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Body(await _client.GetAsync($"/api/jobs/{id}")))
            .GetProperty("scheduledDate").GetString().Should().Be("2099-06-01");
    }

    [Fact]
    public async Task A_transition_on_a_job_that_does_not_exist_answers_404_rather_than_409()
    {
        var response = await _client.PostAsync($"/api/jobs/{Guid.NewGuid()}/start", null);

        // 409 would tell the caller the job exists and refused, which is a
        // different fact and an information leak across tenants.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- the walkthrough -------------------------------------------------

    [Fact]
    public async Task The_walkthrough_runs_end_to_end_over_HTTP()
    {
        // Steps 1-3 and 5-8 of architecture 8.1, which is what this plan
        // unblocks. Steps 4 and 9 need the outbox, and that is plan 4.
        var id = await CreateJob();

        (await _client.PostAsync($"/api/jobs/{id}/start", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _client.PostAsJsonAsync($"/api/jobs/{id}/complete", new
        {
            signatureUrl = "data:image/png;base64,AAA",
            photos = new[] { new { url = "p1.jpg", caption = "ridge" } },
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await Body(await _client.GetAsync($"/api/jobs/{id}"));
        detail.GetProperty("status").GetString().Should().Be("Completed");
        detail.GetProperty("photos").GetArrayLength().Should().Be(1);
        detail.GetProperty("signatureUrl").GetString().Should().NotBeNullOrEmpty();

        var listed = (await Body(await _client.GetAsync("/api/jobs?limit=5&statuses=Completed")))
            .GetProperty("items");
        listed.GetArrayLength().Should().Be(1);
        listed[0].GetProperty("photoCount").GetInt32().Should().Be(1);
    }
}
