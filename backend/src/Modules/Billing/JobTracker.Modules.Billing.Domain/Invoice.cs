using JobTracker.Common.Domain;

namespace JobTracker.Modules.Billing.Domain;

/// <summary>
/// A real aggregate rather than a row a handler fills in (D-04). Its invariants
/// are its own: a positive amount, a labour window that runs forwards, and an
/// issue date it never revises.
///
/// There is no method to change an amount. An issued invoice is a statement to
/// a customer, and correcting one is a credit note — which prd section 9 puts
/// out of scope. Absent is better than present and forbidden.
/// </summary>
public sealed class Invoice : AggregateRoot, ITenantScoped
{
    private Invoice(Guid id) : base(id) { }

    // EF only.
    private Invoice() { }

    public Guid JobId { get; private init; }
    public Guid CustomerId { get; private init; }
    public Guid OrganizationId { get; private init; }
    public decimal Amount { get; private init; }
    public DateTimeOffset StartedAt { get; private init; }

    /// <summary>
    /// Half of the idempotency key (4.5). It is stable across a replay because
    /// a completion timestamp does not change.
    /// </summary>
    public DateTimeOffset JobCompletedAt { get; private init; }

    public DateTimeOffset IssuedAt { get; private init; }

    public static Result<Invoice> Raise(
        Guid jobId,
        Guid customerId,
        Guid organizationId,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        LabourRate rate,
        DateTimeOffset issuedAt)
    {
        if (completedAt < startedAt)
        {
            return Result.Failure<Invoice>(InvoiceErrors.LabourWindowEndsBeforeItBegins);
        }

        var amount = rate.For(completedAt - startedAt);

        if (amount <= 0m)
        {
            return Result.Failure<Invoice>(InvoiceErrors.AmountNotPositive);
        }

        return Result.Success(new Invoice(Guid.NewGuid())
        {
            JobId = jobId,
            CustomerId = customerId,
            OrganizationId = organizationId,
            Amount = amount,
            StartedAt = startedAt,
            JobCompletedAt = completedAt,
            IssuedAt = issuedAt,
        });
    }
}
