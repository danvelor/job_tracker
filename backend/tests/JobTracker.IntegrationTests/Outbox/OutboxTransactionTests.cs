using FluentAssertions;
using JobTracker.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.IntegrationTests.Outbox;

/// <summary>
/// Case 5 of architecture 8.2, and the one a unit test genuinely cannot reach:
/// a unit test shows the interceptor was called, not that the write is one
/// transaction.
/// </summary>
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

        // NFR-2. There is no window in which consequences are recorded for a
        // completion that rolled back — the half of the guarantee a "was the
        // interceptor called" unit test says nothing about.
        (await Context.Jobs.CountAsync()).Should().Be(0);
        (await OutboxRows()).Should().Be(0);
    }

    [Fact]
    public async Task A_rejected_write_leaves_no_outbox_row_behind()
    {
        // A job pointing at an assignee that does not exist. The constraint
        // rejects it, and the interceptor has already staged the outbox row by
        // then. Were the two not one transaction, the row would survive and the
        // system would notify a crew about a job that does not exist.
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

        // D-34. The row's key is the event's own identity, so a replay carries
        // the same source_event_id and the consumers' unique constraints hold
        // without any ambient context telling a handler which row it came from.
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

        // Asserting the row is non-empty would pass on a row nothing can read
        // back. The drain in task 2 depends on this round trip, and finding out
        // there is what makes a poison message rather than a failing test.
        type.Should().Be("JobTracker.Modules.Jobs.Domain.Events.JobCreatedDomainEvent");
    }

    [Fact]
    public async Task The_aggregate_is_left_with_no_events_to_raise_twice()
    {
        var job = AJob();
        Context.Jobs.Add(job);
        await Context.SaveChangesAsync();

        // Cleared by the interceptor after copying. Left in place, a second
        // SaveChanges on the same instance would enqueue them again.
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

        // An interceptor that ran unconditionally would re-enqueue on every
        // save in the request, and the notification would arrive twice.
        (await OutboxRows()).Should().Be(1);
    }
}
