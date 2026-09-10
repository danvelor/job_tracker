using FluentAssertions;
using JobTracker.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.IntegrationTests.Outbox;

public sealed class OutboxTransactionTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static Job AJobAssignedTo(Guid assignee, Guid customer, Guid organization) =>
        Job.Create(
            "Ridge tile replacement", null,
            Address.Create("12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m).Value,
            new DateOnly(2099, 3, 14), assignee, customer, organization, Now).Value;

    private Job AJob() => AJobAssignedTo(Assignee, Customer, Organization);

    private Task<int> OutboxRows() =>
        Context.Database
            .SqlQuery<int>($"""select count(*)::int as "Value" from jobs.outbox_messages""")
            .SingleAsync();

    [Fact]
    public async Task Creating_a_job_writes_its_domain_event_to_the_outbox()
    {
        Context.Jobs.Add(AJob());
        await Context.SaveChangesAsync();

        (await OutboxRows()).Should().Be(1);
    }

    [Fact]
    public async Task A_rollback_leaves_neither_the_job_nor_its_outbox_row()
    {
        await using var transaction = await Context.Database.BeginTransactionAsync();
        Context.Jobs.Add(AJob());
        await Context.SaveChangesAsync();
        await transaction.RollbackAsync();
        Context.ChangeTracker.Clear();

        (await Context.Jobs.CountAsync()).Should().Be(0);
        (await OutboxRows()).Should().Be(0);
    }

    [Fact]
    public async Task A_rejected_write_leaves_no_outbox_row_behind()
    {
        Context.Jobs.Add(AJobAssignedTo(Guid.NewGuid(), Customer, Organization));

        var save = async () => await Context.SaveChangesAsync();

        await save.Should().ThrowAsync<DbUpdateException>();
        Context.ChangeTracker.Clear();
        (await OutboxRows()).Should().Be(0);
    }

    [Fact]
    public async Task The_outbox_row_carries_the_event_identity_as_its_key()
    {
        var job = AJob();
        var eventId = job.DomainEvents.Single().Id;
        Context.Jobs.Add(job);
        await Context.SaveChangesAsync();

        var stored = await Context.Database
            .SqlQuery<Guid>($"""select id as "Value" from jobs.outbox_messages""")
            .SingleAsync();

        stored.Should().Be(eventId);
    }

    [Fact]
    public async Task The_stored_content_deserialises_back_to_the_event_that_was_raised()
    {
        var job = AJob();
        Context.Jobs.Add(job);
        await Context.SaveChangesAsync();

        var type = await Context.Database
            .SqlQuery<string>($"""select type as "Value" from jobs.outbox_messages""")
            .SingleAsync();

        type.Should().Be("JobTracker.Modules.Jobs.Domain.Events.JobCreatedDomainEvent");
    }

    [Fact]
    public async Task The_aggregate_is_left_with_no_events_to_raise_twice()
    {
        var job = AJob();
        Context.Jobs.Add(job);
        await Context.SaveChangesAsync();

        job.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Completing_a_job_enqueues_a_second_event_rather_than_replacing_the_first()
    {
        var job = AJob();
        Context.Jobs.Add(job);
        await Context.SaveChangesAsync();

        job.Start(Now.AddHours(1));
        job.Complete(Now.AddHours(6), "sig", []);
        await Context.SaveChangesAsync();

        (await OutboxRows()).Should().Be(2);
    }

    [Fact]
    public async Task A_save_that_changes_nothing_enqueues_nothing()
    {
        Context.Jobs.Add(AJob());
        await Context.SaveChangesAsync();

        await Context.SaveChangesAsync();

        (await OutboxRows()).Should().Be(1);
    }
}
