namespace rental_ps_smart_billing.Services;

public static class BillingCalculator
{
    /// <summary>
    /// Menit awal Free Play yang tidak ditagih. Diisi dari appsettings Billing:FreePlayGraceMinutes.
    /// </summary>
    public static int FreePlayGraceMinutes { get; set; } = 5;

    /// <summary>
    /// Berapa menit sebelum EndsAt otomatis kirim overlay peringatan ke Tizen. 0 = nonaktif.
    /// </summary>
    public static int SessionWarnMinutesBeforeEnd { get; set; } = 5;

    /// <summary>
    /// Menit tagihan dengan pembulatan matematika standar (≥ 0.5 naik), tanpa grace.
    /// </summary>
    public static int BillableMinutes(DateTime startedAt, DateTime endedAt)
    {
        var elapsed = endedAt - startedAt;
        if (elapsed <= TimeSpan.Zero)
            return 0;

        return (int)Math.Round(elapsed.TotalMinutes, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Menit Free Play setelah dikurangi grace (minimal 0).
    /// </summary>
    public static int BillableOpenEndedMinutes(DateTime startedAt, DateTime endedAt)
    {
        var grace = Math.Max(0, FreePlayGraceMinutes);
        return Math.Max(0, BillableMinutes(startedAt, endedAt) - grace);
    }

    public static decimal CalculateOpenEndedAmount(DateTime startedAt, DateTime endedAt, decimal pricePerMinute)
    {
        var minutes = BillableOpenEndedMinutes(startedAt, endedAt);
        return minutes * pricePerMinute;
    }
}
