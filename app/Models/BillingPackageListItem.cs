namespace rental_ps_smart_billing.Models;

public sealed class BillingPackageListItem
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public int DurationMinutes { get; init; }
    public decimal Price { get; init; }
    public bool IsActive { get; init; }
    public string BillingMode { get; init; } = BillingModes.Fixed;

    public bool IsOpenEnded => BillingModes.IsOpenEnded(BillingMode);

    public string DurationDisplay => IsOpenEnded ? "Free Play" : $"{DurationMinutes} menit";

    public string PriceDisplay => IsOpenEnded
        ? $"Rp {Price:N0}/menit"
        : $"Rp {Price:N0}";

    public string Status => IsActive ? "Aktif" : "Nonaktif";
}
