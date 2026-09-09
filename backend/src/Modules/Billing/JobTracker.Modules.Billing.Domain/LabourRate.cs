namespace JobTracker.Modules.Billing.Domain;

/// <summary>
/// What Billing knows and Jobs must not (D-33): labour is charged by the hour,
/// with a call-out minimum. A completion of eleven minutes still costs the
/// minimum, which is why an amount can be positive for a window that rounds to
/// nothing — and why the CHECK on the column is not the same rule as this one.
/// </summary>
public sealed record LabourRate(decimal PerHour, decimal Minimum)
{
    /// <summary>The default a deployment overrides from configuration.</summary>
    public static readonly LabourRate Standard = new(90m, 100m);

    public decimal For(TimeSpan worked)
    {
        // Two decimals because that is what numeric(12,2) stores. Rounding here
        // rather than letting PostgreSQL do it means the aggregate and the row
        // agree about what was charged.
        var byTheHour = decimal.Round((decimal)worked.TotalHours * PerHour, 2);

        return Math.Max(byTheHour, Minimum);
    }
}
