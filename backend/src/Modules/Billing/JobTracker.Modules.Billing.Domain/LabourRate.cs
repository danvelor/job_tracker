namespace JobTracker.Modules.Billing.Domain;

public sealed record LabourRate(decimal PerHour, decimal Minimum)
{
    public static readonly LabourRate Standard = new(90m, 100m);

    public decimal For(TimeSpan worked)
    {
        var byTheHour = decimal.Round((decimal)worked.TotalHours * PerHour, 2);

        return Math.Max(byTheHour, Minimum);
    }
}
