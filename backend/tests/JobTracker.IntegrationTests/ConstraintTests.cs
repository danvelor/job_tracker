using FluentAssertions;
using JobTracker.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JobTracker.IntegrationTests;

/// <summary>
/// Case 9 of architecture 8.2. The aggregate enforces every one of these too,
/// and that is not enough: a row written by anything other than the aggregate —
/// a migration, a psql session, a future service, a bulk import — must not be
/// able to contradict a business rule the rest of the system relies on.
/// </summary>
public sealed class ConstraintTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private Task<int> InsertRawJob(string status, string? signature = null, string? reason = null) =>
        Context.Database.ExecuteSqlAsync(
            $"""
             insert into jobs.jobs
               (id, organization_id, title, status, street, city, state, zip_code,
                latitude, longitude, assignee_id, customer_id, signature_url,
                cancellation_reason, created_at, updated_at)
             values
               ({Guid.NewGuid()}, {Organization}, 'Written around the aggregate', {status},
                '12 Elm St', 'Springfield', 'IL', '62701', 39.78, -89.65,
                {Assignee}, {Customer}, {signature}, {reason}, now(), now())
             """);

    private async Task SeedOneJob() =>
        await Context.Database.ExecuteSqlAsync(
            $"""
             insert into jobs.jobs
               (id, organization_id, title, status, street, city, state, zip_code,
                latitude, longitude, assignee_id, customer_id, created_at, updated_at)
             values
               ({Guid.NewGuid()}, {Organization}, 'Seeded', 'Scheduled',
                '12 Elm St', 'Springfield', 'IL', '62701', 39.78, -89.65,
                {Assignee}, {Customer}, now(), now())
             """);

    [Fact]
    public async Task A_completed_job_cannot_exist_without_a_signature()
    {
        // BR-4 at the level of the data.
        var write = async () => await InsertRawJob("Completed");

        (await write.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be("ck_jobs_completed_has_signature");
    }

    [Fact]
    public async Task A_completed_job_with_a_signature_is_accepted()
    {
        // The constraint has to permit the legitimate row, or the test above
        // would pass against a table that refuses everything.
        var write = async () => await InsertRawJob("Completed", signature: "data:image/png;base64,AAA");

        await write.Should().NotThrowAsync();
    }

    [Fact]
    public async Task A_cancelled_job_cannot_exist_without_a_reason()
    {
        // BR-5.
        var write = async () => await InsertRawJob("Cancelled");

        (await write.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be("ck_jobs_cancelled_has_reason");
    }

    [Fact]
    public async Task The_status_column_refuses_a_value_outside_the_enum()
    {
        await SeedOneJob();

        // Storing the status as text buys readability; without a CHECK it also
        // buys the freedom to store nonsense, which an integer enum at least
        // did not allow.
        var write = async () => await Context.Database.ExecuteSqlAsync(
            $"update jobs.jobs set status = 'Elsewhere'");

        (await write.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be("ck_jobs_status");
    }

    [Fact]
    public async Task Every_status_the_domain_defines_is_accepted_by_the_constraint()
    {
        await SeedOneJob();

        foreach (var status in Enum.GetValues<JobStatus>())
        {
            var write = async () => await Context.Database.ExecuteSqlAsync(
                $"""
                 update jobs.jobs
                 set status = {status.ToString()}, signature_url = 'sig',
                     cancellation_reason = 'reason'
                 """);

            // Enumerating the enum rather than listing five literals: adding a
            // JobStatus without extending the constraint fails here, which is
            // the only place that pairing is checked.
            await write.Should().NotThrowAsync($"{status} is a status the domain defines");
        }
    }

    [Fact]
    public async Task A_job_cannot_reference_an_assignee_that_does_not_exist()
    {
        var stranger = Guid.NewGuid();

        var write = async () => await Context.Database.ExecuteSqlAsync(
            $"""
             insert into jobs.jobs
               (id, organization_id, title, status, street, city, state, zip_code,
                latitude, longitude, assignee_id, customer_id, created_at, updated_at)
             values
               ({Guid.NewGuid()}, {Organization}, 'Ghost crew', 'Scheduled',
                '12 Elm St', 'Springfield', 'IL', '62701', 39.78, -89.65,
                {stranger}, {Customer}, now(), now())
             """);

        // D-26 reversed D-21 precisely so this fails. A job assigned to nobody
        // real is a job nobody does.
        await write.Should().ThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task Deleting_a_job_takes_its_photos_with_it()
    {
        var job = Job.Create(
            "With photos", null,
            Address.Create("12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m).Value,
            new DateOnly(2099, 5, 1), Assignee, Customer, Organization, Now).Value;
        job.Start(Now);
        job.Complete(Now, "sig", [new NewJobPhoto("a.jpg", Now, null)]);
        Context.Jobs.Add(job);
        await Context.SaveChangesAsync();

        await Context.Database.ExecuteSqlAsync($"delete from jobs.jobs where id = {job.Id}");

        // BR-2 means the application never deletes a job. The cascade is for
        // the day a database is trimmed by hand: a photo whose job is gone is
        // a row nothing can ever reach.
        var orphans = await Context.Database
            .SqlQuery<int>($"""select count(*)::int as "Value" from jobs.job_photos""")
            .SingleAsync();

        orphans.Should().Be(0);
    }

    [Fact]
    public async Task The_seeded_rosters_are_present_for_the_development_organization()
    {
        var assignees = await Context.Assignees.CountAsync();
        var customers = await Context.Customers.CountAsync();

        // Every walkthrough step that picks a crew or a customer depends on
        // these existing. A missing seed would fail the smoke run at step 2,
        // several layers away from the cause.
        assignees.Should().Be(2);
        customers.Should().Be(2);
    }
}
