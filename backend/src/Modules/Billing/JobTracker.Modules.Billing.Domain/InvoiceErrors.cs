using JobTracker.Common.Domain;

namespace JobTracker.Modules.Billing.Domain;

public static class InvoiceErrors
{
    public static readonly Error LabourWindowEndsBeforeItBegins = Error.Validation(
        "invoice.labour-window-inverted",
        "A job cannot be completed before it started");

    /// <summary>
    /// The column has a CHECK for this too. Refusing here names the cause;
    /// letting it reach the database surfaces a constraint violation that says
    /// only that something was wrong.
    /// </summary>
    public static readonly Error AmountNotPositive = Error.Validation(
        "invoice.amount-not-positive",
        "There is nothing to charge for this job");
}
