using FluentAssertions;
using JobTracker.Common.Domain;
using JobTracker.Modules.Billing.Domain;

namespace JobTracker.Modules.Billing.UnitTests;

public sealed class InvoiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Job = Guid.NewGuid();
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly Guid Organization = Guid.NewGuid();

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
        Raise(TimeSpan.FromMinutes(5)).Value.Amount.Should().Be(100m);
    }

    [Fact]
    public void A_part_hour_beyond_the_minimum_is_charged_pro_rata()
    {
        Raise(TimeSpan.FromMinutes(90)).Value.Amount.Should().Be(135m);
    }

    [Fact]
    public void The_amount_is_rounded_to_the_currency_the_column_stores()
    {
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
        Invoice.Raise(Job, Customer, Organization, Start, Start.AddHours(2), new LabourRate(0m, 0m), Start)
            .Error.Should().Be(InvoiceErrors.AmountNotPositive);
    }

    [Fact]
    public void An_invoice_records_the_window_it_was_priced_from()
    {
        var invoice = Raise(TimeSpan.FromHours(2)).Value;

        invoice.StartedAt.Should().Be(Start);
        invoice.JobCompletedAt.Should().Be(Start.AddHours(2));
    }

    [Fact]
    public void An_invoice_has_no_way_to_change_its_amount_after_it_is_issued()
    {
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
