namespace rental_ps_smart_billing.Models;

public sealed class RentalSession
{
    public int Id { get; init; }
    public int SmartTvId { get; init; }
    public int? PackageId { get; init; }
    public string? CustomerName { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public DateTime? EndedAt { get; init; }
    public DateTime? OpenEndedFrom { get; init; }
    public required string Status { get; init; }
    public decimal? Amount { get; init; }
    public int? StartedByUserId { get; init; }
    public string? PackageName { get; init; }
    public decimal? PackagePrice { get; init; }
    public string BillingMode { get; init; } = BillingModes.Fixed;

    public bool IsOpenEnded => BillingModes.IsOpenEnded(BillingMode);

    /// <summary>Titik mulai perhitungan Free Play: setelah convert, atau StartedAt jika Free Play murni.</summary>
    public DateTime OpenEndedBillingFrom => OpenEndedFrom ?? StartedAt;
}
