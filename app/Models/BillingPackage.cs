namespace rental_ps_smart_billing.Models;

public sealed class BillingPackage
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public int DurationMinutes { get; init; }
    public decimal Price { get; init; }
    public bool IsActive { get; init; }

    public string DisplayLabel => $"{Name} — Rp {Price:N0}";
}
