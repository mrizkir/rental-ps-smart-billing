namespace rental_ps_smart_billing.Models;

public sealed class AutoEndedSessionItem
{
    public int SessionId { get; init; }
    public required string TvName { get; init; }
    public string? CustomerName { get; init; }
    public decimal Amount { get; init; }
    public string? WarningMessage { get; init; }

    public string CustomerDisplay =>
        string.IsNullOrWhiteSpace(CustomerName) ? "-" : CustomerName;

    public string AmountDisplay => $"Rp {Amount:N0}";

    public string LineDisplay =>
        string.IsNullOrWhiteSpace(CustomerName)
            ? $"{TvName} — {AmountDisplay}"
            : $"{TvName} ({CustomerName}) — {AmountDisplay}";
}
