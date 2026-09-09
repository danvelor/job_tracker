using FluentAssertions;
using JobTracker.Modules.Billing.Application;
using JobTracker.Modules.Billing.Domain;
using JobTracker.Modules.Billing.Infrastructure;
using JobTracker.Modules.Billing.Infrastructure.Repositories;
using JobTracker.Modules.Jobs.IntegrationEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobTracker.IntegrationTests.Pipeline;

/// <summary>
/// FR-9 against a real database, including case 9 of architecture 8.2: the
/// unique constraint is the whole of 4.5, and a test that never inserts twice
/// never checks it.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class BillingPipelineTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Started = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Organization = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private BillingDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _context = new BillingDbContext(
            new DbContextOptionsBuilder<BillingDbContext>()
                .UseNpgsql(postgres.ConnectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", BillingDbContext.Schema))
                .UseSnakeCaseNamingConvention()
                .Options);

        await _context.Database.MigrateAsync();
        await _context.Database.ExecuteSqlRawAsync("truncate billing.invoices");
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    private static JobCompletedIntegrationEvent ACompletion(
        Guid? jobId = null, double hours = 2, Guid? eventId = null) =>
        new(eventId ?? Guid.NewGuid(),
            jobId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            Organization,
            Started,
            Started.AddHours(hours));

    private GenerateInvoiceOnJobCompletedHandler Handler() =>
        new(new InvoiceRepository(_context),
            new BillingUnitOfWork(_context),
            Options.Create(new BillingOptions()),
            TimeProvider.System);

    [Fact]
    public async Task Completing_a_job_raises_an_invoice()
    {
        await Handler().HandleAsync(ACompletion());

        (await _context.Invoices.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task The_amount_comes_from_the_labour_window_and_Billings_own_rate()
    {
        await Handler().HandleAsync(ACompletion(hours: 2));

        // Jobs sent no amount and could not have: it does not know the rate
        // (D-33). This is the boundary paying for itself.
        (await _context.Invoices.SingleAsync()).Amount.Should().Be(180m);
    }

    [Fact]
    public async Task A_replayed_completion_raises_no_second_invoice()
    {
        var completion = ACompletion();

        await Handler().HandleAsync(completion);
        _context.ChangeTracker.Clear();
        await Handler().HandleAsync(completion);

        // uq_invoices_idempotency (job_id, job_completed_at) — the key line 246
        // asks for by name. Both parts survive a replay, which is the condition
        // that makes the rule sufficient.
        (await _context.Invoices.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task The_unique_constraint_is_real_and_not_only_the_handler_being_careful()
    {
        var completion = ACompletion();
        await Handler().HandleAsync(completion);
        _context.ChangeTracker.Clear();

        _context.Invoices.Add(Invoice.Raise(
            completion.JobId, completion.CustomerId, Organization,
            completion.StartedAt, completion.CompletedAt, LabourRate.Standard, Started).Value);

        var save = async () => await _context.SaveChangesAsync();

        // The handler's check is a courtesy. This is what stops two workers
        // racing past it in the same instant.
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task A_second_completion_of_the_same_job_does_raise_a_second_invoice()
    {
        // The other side of the key, and the reason it is a pair. BR-2 makes a
        // real re-completion impossible today, but a key that ignored the
        // timestamp would silently swallow one if it ever became possible.
        var jobId = Guid.NewGuid();

        await Handler().HandleAsync(ACompletion(jobId, hours: 2));
        _context.ChangeTracker.Clear();
        await Handler().HandleAsync(ACompletion(jobId, hours: 3));

        (await _context.Invoices.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task An_uninvoiceable_completion_is_skipped_rather_than_retried_for_ever()
    {
        // A window that runs backwards cannot be priced, and never will be.
        // Returning quietly lets the outbox row be stamped so the notification
        // handlers on the same event are not blocked behind it.
        var inverted = new JobCompletedIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Organization,
            Started, Started.AddHours(-1));

        var handle = async () => await Handler().HandleAsync(inverted);

        await handle.Should().NotThrowAsync();
        (await _context.Invoices.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Billing_writes_only_into_its_own_schema()
    {
        await Handler().HandleAsync(ACompletion());

        var tables = await _context.Database
            .SqlQuery<string>(
                $"""
                 select table_name as "Value" from information_schema.tables
                 where table_schema = 'billing'
                 """)
            .ToListAsync();

        // Architecture 6.1. A module that writes into another's schema has a
        // boundary in the diagram and none in the database.
        tables.Should().BeEquivalentTo(["invoices", "__EFMigrationsHistory"]);
    }

    [Fact]
    public async Task There_is_no_foreign_key_from_an_invoice_to_a_job()
    {
        var constraints = await _context.Database
            .SqlQuery<string>(
                $"""
                 select conname as "Value" from pg_constraint
                 where connamespace = 'billing'::regnamespace and contype = 'f'
                 """)
            .ToListAsync();

        // The absence is the module boundary itself. A constraint here would
        // let the database enforce a relationship the two modules express
        // through a contract, and would turn extracting Billing into its own
        // database from a migration into a redesign.
        constraints.Should().BeEmpty();
    }
}
