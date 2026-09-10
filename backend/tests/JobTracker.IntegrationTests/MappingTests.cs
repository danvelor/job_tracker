using FluentAssertions;
using JobTracker.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.IntegrationTests;

public sealed class MappingTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static Job AJob() =>
        Job.Create(
            "Ridge tile replacement", "Replace cracked ridge tiles",
            Address.Create("12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m).Value,
            new DateOnly(2099, 3, 14), Assignee, Customer, Organization, Now).Value;

    [Fact]
    public async Task An_address_round_trips_through_six_flattened_columns()
    {
        var job = AJob();
        Context.Jobs.Add(job);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var read = await Context.Jobs.SingleAsync();

        read.Address.Should().Be(job.Address);
    }

    [Fact]
    public async Task The_address_is_stored_as_columns_rather_than_as_a_table()
    {
        var tables = await Context.Database
            .SqlQuery<string>(
                $"""select table_name as "Value" from information_schema.tables where table_schema = 'jobs'""")
            .ToListAsync();

        tables.Should().Contain("jobs");
        tables.Should().NotContain("address");
    }

    [Fact]
    public async Task The_status_is_stored_as_text_rather_than_as_an_ordinal()
    {
        Context.Jobs.Add(AJob());
        await Context.SaveChangesAsync();

        var status = await Context.Database
            .SqlQuery<string>($"""select status as "Value" from jobs.jobs limit 1""")
            .SingleAsync();

        status.Should().Be("Scheduled");
    }

    [Fact]
    public async Task Column_names_are_snake_case()
    {
        var columns = await Context.Database
            .SqlQuery<string>(
                $"""
                 select column_name as "Value" from information_schema.columns
                 where table_schema = 'jobs' and table_name = 'jobs'
                 """)
            .ToListAsync();

        columns.Should().Contain(["organization_id", "scheduled_date", "zip_code", "created_at"]);
        columns.Should().NotContain("OrganizationId");
    }

    [Fact]
    public async Task Photos_round_trip_with_their_job()
    {
        var job = AJob();
        job.Start(Now.AddHours(1));
        job.Complete(Now.AddHours(6), "sig", [new NewJobPhoto("p1.jpg", Now, "ridge")]);
        Context.Jobs.Add(job);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var read = await Context.Jobs.Include(j => j.Photos).SingleAsync();

        read.Photos.Should().ContainSingle().Which.Caption.Should().Be("ridge");
    }

    [Fact]
    public void The_domain_events_are_not_a_mapped_navigation()
    {
        Context.Model.FindEntityType(typeof(Job))!
            .FindNavigation(nameof(Job.DomainEvents))
            .Should().BeNull();
    }

    [Fact]
    public async Task Timestamps_are_recorded_on_insert()
    {
        Context.Jobs.Add(AJob());
        await Context.SaveChangesAsync();

        var createdAt = await Context.Database
            .SqlQuery<DateTimeOffset>($"""select created_at as "Value" from jobs.jobs limit 1""")
            .SingleAsync();

        createdAt.Should().NotBe(default);
    }
}
