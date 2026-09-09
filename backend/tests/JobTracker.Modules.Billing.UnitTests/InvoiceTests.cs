using FluentAssertions;
using JobTracker.Common.Domain;
using JobTracker.Modules.Billing.Domain;

namespace JobTracker.Modules.Billing.UnitTests;

/// <summary>
/// Billing has a real domain rather than a stub handler (D-04), and this is
/// what that buys: pricing is a rule with edges, and the edges are here rather
/// than in a handler nobody tests.
/// </summary>
public sealed class InvoiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Job = Guid.NewGuid();
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly Guid Organization = Guid.NewGuid();

    /// <summary>Ninety an hour, with a call-out minimum of one hundred.</summary>
    private static readonly LabourRate Rate = new(90m, 100m);

    private static Result<Invoice> Raise(TimeSpan worked, DateTimeOffset? issuedAt = null) =>
        Invoice.Raise(
            Job, Customer, Organization, Start, Start + worked, Rate,
            issuedAt ?? Start + worked);

    [Fact]
    public void Two_hours_of_labour_costs_twice_the_hourly_rate()
    {
        Raise(TimeSpan.FromHours(2)).Value.Amount.Should().Be(180m);
    }

    [Fact]
    public void A_five_minute_job_still_costs_the_call_out_minimum()
    {
        // The rule that makes the amount positive for a window that rounds to
        // nothing — and the reason the CHECK on the column is not this rule.
        Raise(TimeSpan.FromMinutes(5)).Value.Amount.Should().Be(100m);
    }

    [Fact]
    public void A_part_hour_beyond_the_minimum_is_charged_pro_rata()
    {
        // Ninety minutes at ninety an hour is 135, above the minimum.
        Raise(TimeSpan.FromMinutes(90)).Value.Amount.Should().Be(135m);
    }

    [Fact]
    public void The_amount_is_rounded_to_the_currency_the_column_stores()
    {
        // numeric(12,2). An amount with more precision than the column would be
        // rounded by PostgreSQL instead, so the row and the aggregate would
        // disagree about what was charged.
        var amount = Raise(TimeSpan.FromMinutes(7)).Value.Amount;

        amount.Should().Be(decimal.Round(amount, 2));
    }

    [Fact]
    public void A_completion_before_its_start_is_refused_rather_than_priced_negative()
    {
        Invoice.Raise(Job, Customer, Organization, Start, Start.AddHours(-1), Rate, Start)
            .Error.Should().Be(InvoiceErrors.LabourWindowEndsBeforeItBegins);
    }

    [Fact]
    public void A_rate_that_would_produce_nothing_to_charge_is_refused()
    {
        // A zero amount is not an invoice; it is a bug that reached the
        // database, and the CHECK would reject it anyway. Refusing here names
        // the cause instead of surfacing a constraint violation.
        Invoice.Raise(Job, Customer, Organization, Start, Start.AddHours(2), new LabourRate(0m, 0m), Start)
            .Error.Should().Be(InvoiceErrors.AmountNotPositive);
    }

    [Fact]
    public void An_invoice_records_the_window_it_was_priced_from()
    {
        // Without it nobody can answer "why this amount" six months later, and
        // an invoice nobody can explain is an invoice nobody can defend.
        var invoice = Raise(TimeSpan.FromHours(2)).Value;

        invoice.StartedAt.Should().Be(Start);
        invoice.JobCompletedAt.Should().Be(Start.AddHours(2));
    }

    [Fact]
    public void An_invoice_has_no_way_to_change_its_amount_after_it_is_issued()
    {
        // An issued invoice is a statement to a customer. Correcting one is a
        // credit note, which prd section 9 puts out of scope — so there is no
        // setter rather than a setter nobody is supposed to call.
        typeof(Invoice).GetProperties()
            .Where(property => property.CanWrite && property.SetMethod!.IsPublic)
            .Should().BeEmpty();
    }

    [Fact]
    public void The_issue_date_comes_from_the_caller_rather_than_from_a_clock()
    {
        var issuedAt = Start.AddDays(1);

        Raise(TimeSpan.FromHours(2), issuedAt).Value.IssuedAt.Should().Be(issuedAt);
    }
}
