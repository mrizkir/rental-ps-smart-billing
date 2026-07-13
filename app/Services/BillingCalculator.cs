namespace rental_ps_smart_billing.Services;

public static class BillingCalculator
{
    /// <summary>
    /// Menit tagihan dengan pembulatan matematika standar (≥ 0.5 naik).
    /// </summary>
    public static int BillableMinutes(DateTime startedAt, DateTime endedAt)
    {
        var elapsed = endedAt - startedAt;
        if (elapsed <= TimeSpan.Zero)
            return 0;

        return (int)Math.Round(elapsed.TotalMinutes, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateOpenEndedAmount(DateTime startedAt, DateTime endedAt, decimal pricePerMinute)
    {
        var minutes = BillableMinutes(startedAt, endedAt);
        return minutes * pricePerMinute;
    }
}
