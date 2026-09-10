using FluentAssertions;
using JobTracker.Modules.Jobs.Domain;
using JobTracker.Modules.Jobs.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure.Configurations;
using JobTracker.Modules.Jobs.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.IntegrationTests;

public sealed class SearchTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private JobRepository Repository() => new(Context);

    private static readonly Guid OtherAssigneeSameTenant = RosterSeed.AssigneeRuiz;

    private static JobSearchCriteria Criteria(
        int limit = 10,
        string? cursor = null,
        string? text = null,
        IReadOnlyList<JobStatus>? statuses = null,
        Guid? assigneeId = null,
        JobSortField sort = JobSortField.ScheduledDate) =>
        new(Organization, text, statuses, null, null, assigneeId,
            sort, cursor, limit);

    private async Task Seed(params (string Title, string? Description, DateOnly Date)[] jobs)
    {
        foreach (var (title, description, date) in jobs)
        {
            Context.Jobs.Add(Job.Create(
                title, description,
                Address.Create("12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m).Value,
                date, Assignee, Customer, Organization, Now).Value);
        }

        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    private Task Undate(string title) =>
        Context.Database.ExecuteSqlAsync(
            $"update jobs.jobs set scheduled_date = null where title = {title}");

    [Fact]
    public async Task It_returns_the_newest_scheduled_date_first()
    {
        await Seed(
            ("Older", null, new DateOnly(2099, 1, 1)),
            ("Newer", null, new DateOnly(2099, 6, 1)));

        var rows = await Repository().SearchAsync(Criteria());

        rows.Select(row => row.Title).Should().Equal("Newer", "Older");
    }

    [Fact]
    public async Task Successive_keyset_pages_are_disjoint_and_complete()
    {
        await Seed(
            ("A", null, new DateOnly(2099, 5, 1)),
            ("B", null, new DateOnly(2099, 4, 1)),
            ("C", null, new DateOnly(2099, 3, 1)),
            ("D", null, new DateOnly(2099, 2, 1)));

        var first = await Repository().SearchAsync(Criteria(limit: 2));
        var second = await Repository().SearchAsync(
            Criteria(limit: 2, cursor: first[^1].Id.ToString()));

        first.Select(row => row.Title).Should().Equal("A", "B");
        second.Select(row => row.Title).Should().Equal("C", "D");
    }

    [Fact]
    public async Task Sorting_by_title_orders_alphabetically_rather_than_by_date()
    {
        await Seed(
            ("Zinc flashing", null, new DateOnly(2099, 6, 1)),
            ("Apex ridge", null, new DateOnly(2099, 1, 1)));

        var rows = await Repository().SearchAsync(Criteria(sort: JobSortField.Title));

        rows.Select(row => row.Title).Should().Equal("Apex ridge", "Zinc flashing");
    }

    [Fact]
    public async Task Successive_title_pages_are_disjoint_and_complete()
    {
        await Seed(
            ("Apex ridge", null, new DateOnly(2099, 1, 1)),
            ("Barge board", null, new DateOnly(2099, 5, 1)),
            ("Chimney flaunching", null, new DateOnly(2099, 2, 1)),
            ("Dormer cheek", null, new DateOnly(2099, 4, 1)));

        var first = await Repository().SearchAsync(Criteria(limit: 2, sort: JobSortField.Title));
        var second = await Repository().SearchAsync(
            Criteria(limit: 2, cursor: first[^1].Id.ToString(), sort: JobSortField.Title));

        first.Select(row => row.Title).Should().Equal("Apex ridge", "Barge board");
        second.Select(row => row.Title).Should().Equal("Chimney flaunching", "Dormer cheek");
    }

    [Fact]
    public async Task Two_jobs_sharing_a_title_are_neither_skipped_nor_repeated()
    {
        await Seed(
            ("Repitch", null, new DateOnly(2099, 5, 1)),
            ("Repitch", null, new DateOnly(2099, 4, 1)),
            ("Repitch", null, new DateOnly(2099, 3, 1)),
            ("Repitch", null, new DateOnly(2099, 2, 1)));

        var first = await Repository().SearchAsync(Criteria(limit: 2, sort: JobSortField.Title));
        var second = await Repository().SearchAsync(
            Criteria(limit: 2, cursor: first[^1].Id.ToString(), sort: JobSortField.Title));

        first.Select(row => row.Id).Should().NotIntersectWith(second.Select(row => row.Id));
        first.Concat(second).Should().HaveCount(4);
    }

    [Fact]
    public async Task Two_jobs_sharing_a_date_are_neither_skipped_nor_repeated()
    {
        var sameDay = new DateOnly(2099, 5, 1);
        await Seed(
            ("A", null, sameDay), ("B", null, sameDay),
            ("C", null, sameDay), ("D", null, sameDay));

        var first = await Repository().SearchAsync(Criteria(limit: 2));
        var second = await Repository().SearchAsync(
            Criteria(limit: 2, cursor: first[^1].Id.ToString()));

        first.Select(row => row.Id).Should().NotIntersectWith(second.Select(row => row.Id));
        first.Concat(second).Should().HaveCount(4);
    }

    [Fact]
    public async Task A_dateless_job_loses_nothing_when_the_whole_list_is_paged()
    {
        await Seed(
            ("Dated A", null, new DateOnly(2099, 5, 1)),
            ("Dated B", null, new DateOnly(2099, 4, 1)),
            ("Undated", null, new DateOnly(2099, 3, 1)));
        await Undate("Undated");

        var seen = new List<string>();
        string? cursor = null;

        for (var page = 0; page < 6; page++)
        {
            var rows = await Repository().SearchAsync(Criteria(limit: 1, cursor: cursor));
            if (rows.Count == 0)
            {
                break;
            }

            seen.AddRange(rows.Select(row => row.Title));
            cursor = rows[^1].Id.ToString();
        }

        seen.Should().BeEquivalentTo(["Dated A", "Dated B", "Undated"]);
    }

    [Fact]
    public async Task An_undated_job_sorts_last_rather_than_first()
    {
        await Seed(
            ("Dated", null, new DateOnly(2099, 5, 1)),
            ("Undated", null, new DateOnly(2099, 4, 1)));
        await Undate("Undated");

        var rows = await Repository().SearchAsync(Criteria());

        rows.Select(row => row.Title).Should().Equal("Dated", "Undated");
    }

    [Fact]
    public async Task Full_text_search_matches_the_title_and_the_description()
    {
        await Seed(
            ("Ridge tile replacement", "north slope", new DateOnly(2099, 5, 1)),
            ("Gutter reline", "rear gutter run", new DateOnly(2099, 4, 1)));

        var byTitle = await Repository().SearchAsync(Criteria(text: "ridge"));
        var byDescription = await Repository().SearchAsync(Criteria(text: "slope"));

        byTitle.Should().ContainSingle().Which.Title.Should().Be("Ridge tile replacement");
        byDescription.Should().ContainSingle().Which.Title.Should().Be("Ridge tile replacement");
    }

    [Fact]
    public async Task Full_text_search_matches_a_stemmed_form()
    {
        await Seed(("Gutter reline", "replacing three gutters", new DateOnly(2099, 5, 1)));

        var rows = await Repository().SearchAsync(Criteria(text: "replace"));

        rows.Should().ContainSingle();
    }

    [Fact]
    public async Task Filtering_by_several_statuses_returns_all_of_them()
    {
        await Seed(
            ("A", null, new DateOnly(2099, 5, 1)),
            ("B", null, new DateOnly(2099, 4, 1)));
        await Context.Database.ExecuteSqlAsync(
            $"update jobs.jobs set status = 'Cancelled', cancellation_reason = 'Weather' where title = 'B'");

        var rows = await Repository().SearchAsync(
            Criteria(statuses: [JobStatus.Scheduled, JobStatus.Cancelled]));

        rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task Filtering_by_assignee_narrows_the_page()
    {
        await Seed(("A", null, new DateOnly(2099, 5, 1)));

        var mine = await Repository().SearchAsync(Criteria(assigneeId: Assignee));
        var theirs = await Repository().SearchAsync(Criteria(assigneeId: OtherAssigneeSameTenant));

        mine.Should().ContainSingle();
        theirs.Should().BeEmpty();
    }

    [Fact]
    public async Task Each_row_carries_the_assignee_name_rather_than_only_an_identifier()
    {
        await Seed(("A", null, new DateOnly(2099, 5, 1)));

        var rows = await Repository().SearchAsync(Criteria());

        rows.Single().AssigneeName.Should().Be("J. Ortiz");
    }

    [Fact]
    public async Task The_photo_count_is_right_per_row()
    {
        var withPhotos = Job.Create(
            "With photos", null,
            Address.Create("12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m).Value,
            new DateOnly(2099, 5, 1), Assignee, Customer, Organization, Now).Value;
        withPhotos.Start(Now);
        withPhotos.Complete(Now, "sig",
            [new NewJobPhoto("a.jpg", Now, null), new NewJobPhoto("b.jpg", Now, null)]);
        Context.Jobs.Add(withPhotos);
        await Seed(("No photos", null, new DateOnly(2099, 4, 1)));

        var rows = await Repository().SearchAsync(Criteria());

        rows.Single(row => row.Title == "With photos").PhotoCount.Should().Be(2);
        rows.Single(row => row.Title == "No photos").PhotoCount.Should().Be(0);
    }

    [Fact]
    public async Task The_read_side_tracks_nothing()
    {
        await Seed(("A", null, new DateOnly(2099, 5, 1)));

        await Repository().SearchAsync(Criteria());

        Context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task The_search_is_confined_to_the_tenant()
    {
        await Seed(("Ours", null, new DateOnly(2099, 5, 1)));
        await using var seeding = ContextFor(OtherOrganization);
        seeding.Jobs.Add(Job.Create(
            "Theirs", null,
            Address.Create("12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m).Value,
            new DateOnly(2099, 6, 1), OtherAssignee, OtherCustomer, OtherOrganization, Now).Value);
        await seeding.SaveChangesAsync();

        var rows = await Repository().SearchAsync(Criteria());

        rows.Select(row => row.Title).Should().Equal("Ours");
    }

    [Fact]
    public async Task GetByIdAsync_loads_the_photos_the_aggregate_needs()
    {
        var job = Job.Create(
            "With photos", null,
            Address.Create("12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m).Value,
            new DateOnly(2099, 5, 1), Assignee, Customer, Organization, Now).Value;
        job.Start(Now);
        job.Complete(Now, "sig", [new NewJobPhoto("a.jpg", Now, "ridge")]);
        Context.Jobs.Add(job);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var loaded = await Repository().GetByIdAsync(job.Id);

        loaded!.Photos.Should().ContainSingle();
        Context.ChangeTracker.Entries().Should().NotBeEmpty();
    }

    [Fact]
    public async Task AddAsync_persists_through_the_unit_of_work_rather_than_by_itself()
    {
        var job = Job.Create(
            "New", null,
            Address.Create("12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m).Value,
            new DateOnly(2099, 5, 1), Assignee, Customer, Organization, Now).Value;

        await Repository().AddAsync(job);
        var beforeSave = await Context.Jobs.AsNoTracking().CountAsync();
        await new UnitOfWork(Context).SaveChangesAsync();
        var afterSave = await Context.Jobs.AsNoTracking().CountAsync();

        beforeSave.Should().Be(0);
        afterSave.Should().Be(1);
    }

    [Fact]
    public async Task A_photo_added_to_an_already_persisted_job_is_inserted_rather_than_updated()
    {
        var job = Job.Create(
            "Ridge tile replacement", null,
            Address.Create("12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m).Value,
            new DateOnly(2099, 5, 1), Assignee, Customer, Organization, Now).Value;
        Context.Jobs.Add(job);
        await Context.SaveChangesAsync();

        job.Start(Now);
        job.Complete(Now, "sig", [new NewJobPhoto("a.jpg", Now, "ridge")]);
        await new UnitOfWork(Context).SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var reloaded = await Repository().GetByIdAsync(job.Id);

        reloaded!.Photos.Should().ContainSingle().Which.Caption.Should().Be("ridge");
    }

    [Fact]
    public async Task Updating_a_persisted_job_through_SaveChanges_succeeds_despite_the_trigger()
    {
        var job = Job.Create(
            "Ridge tile replacement", null,
            Address.Create("12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m).Value,
            new DateOnly(2099, 5, 1), Assignee, Customer, Organization, Now).Value;
        Context.Jobs.Add(job);
        await Context.SaveChangesAsync();

        job.Start(Now.AddHours(1));
        var save = async () => await new UnitOfWork(Context).SaveChangesAsync();

        await save.Should().NotThrowAsync();
    }
}
