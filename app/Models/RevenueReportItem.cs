namespace rental_ps_smart_billing.Models;

public sealed class RevenueReportItem
{
    public int SessionId { get; init; }
    public required string TvName { get; init; }
    public string? CustomerName { get; init; }
    public string? PackageName { get; init; }
    public string BillingMode { get; init; } = BillingModes.Fixed;
    public DateTime StartedAt { get; init; }
    public DateTime EndedAt { get; init; }
    public decimal Amount { get; init; }

    public string CustomerDisplay =>
        string.IsNullOrWhiteSpace(CustomerName) ? "-" : CustomerName;

    public string PackageDisplay =>
        string.IsNullOrWhiteSpace(PackageName)
            ? (BillingModes.IsOpenEnded(BillingMode) ? "Free Play" : "-")
            : PackageName;

    public string StartedLocalDisplay => StartedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public string EndedLocalDisplay => EndedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public string AmountDisplay => $"Rp {Amount:N0}";
}

public sealed class RevenueReportResult
{
    public DateTime FromLocalDate { get; init; }
    public DateTime ToLocalDate { get; init; }
    public int SessionCount { get; init; }
    public decimal TotalAmount { get; init; }
    public IReadOnlyList<RevenueReportItem> Items { get; init; } = [];

    public string TotalAmountDisplay => $"Rp {TotalAmount:N0}";
    public string PeriodDisplay =>
        FromLocalDate.Date == ToLocalDate.Date
            ? FromLocalDate.ToString("dd MMM yyyy")
            : $"{FromLocalDate:dd MMM yyyy} – {ToLocalDate:dd MMM yyyy}";
}
