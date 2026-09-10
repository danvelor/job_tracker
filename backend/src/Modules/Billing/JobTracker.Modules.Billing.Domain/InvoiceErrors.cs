using JobTracker.Common.Domain;

namespace JobTracker.Modules.Billing.Domain;

public static class InvoiceErrors
{
    public static readonly Error LabourWindowEndsBeforeItBegins = Error.Validation(
        "invoice.labour-window-inverted",
        "A job cannot be completed before it started");

    public static readonly Error AmountNotPositive = Error.Validation(
        "invoice.amount-not-positive",
        "There is nothing to charge for this job");
}
